"""P10 (CA-P10-01 a 12, D-87, D-91 A): genera los derivados web del logo en frontend/public/marca/.

Uso (desde cualquier carpeta):  python frontend/scripts/generar-marca.py
Requiere Python 3 con Pillow (soporte WebP). No es parte del build: los derivados se versionan en Git y el
script solo se vuelve a ejecutar si cambian los originales (por ejemplo, cuando llegue el horizontal con
transparencia, D-91 a B).

Los originales de docs/marca/ solo se leen: nunca se modifican, renombran ni copian a public/.
"""
from __future__ import annotations

import io
import sys
from pathlib import Path

from PIL import Image

RAIZ = Path(__file__).resolve().parents[2]
ORIGEN = RAIZ / "docs" / "marca"
HORIZONTAL = ORIGEN / "WhatsApp Image 2026-11-16 at 8.54.30 PM.jpeg"
CUADRADO = ORIGEN / "Logo VCA Automotores Renovado.png"
PUBLICO = RAIZ / "frontend" / "public"
DESTINO = PUBLICO / "marca"

# Pesos máximos de la tabla de §2.1 (bytes).
KB = 1024
LIMITES = {
    "logo-horizontal.webp": 30 * KB,
    "logo-horizontal.png": 60 * KB,
    "logo-cuadrado.webp": 60 * KB,
    "logo-cuadrado.png": 120 * KB,
    "og-portal.jpg": 200 * KB,
    "favicon-32.png": 10 * KB,
    "apple-touch-icon.png": 10 * KB,
    "icon-192.png": 10 * KB,
    "icon-512.png": 60 * KB,
}

# D-91 a A: el JPEG trae el logo en la franja superior; debajo hay una franja gris (y≈330-390) y margen blanco.
# Solo se busca el contenido por encima de esa franja.
LIMITE_FRANJA_GRIS = 328
UMBRAL_CONTENIDO = 235  # canal mínimo por debajo del cual el píxel no es fondo blanco
MARGEN = 6

# D-91 b A: cabeza del toro (cuernos, gorra y hocico) en el logo cuadrado, en píxeles del original 1312x1199.
CAJA_TORO = (408, 0, 940, 312)


def recortar_horizontal() -> Image.Image:
    im = Image.open(HORIZONTAL).convert("RGB")
    ancho, _ = im.size
    zona = im.crop((0, 0, ancho, LIMITE_FRANJA_GRIS))
    # Máscara de contenido: algún canal claramente por debajo del blanco.
    gris = zona.convert("L").point(lambda v: 255 if v < UMBRAL_CONTENIDO else 0)
    caja = gris.getbbox()
    if caja is None:
        sys.exit("No se encontró contenido en el logo horizontal.")
    x0, y0, x1, y1 = caja
    caja = (max(0, x0 - MARGEN), max(0, y0 - MARGEN), min(ancho, x1 + MARGEN), min(LIMITE_FRANJA_GRIS, y1 + MARGEN))
    recorte = im.crop(caja)
    # El fondo del JPEG es casi blanco (252-253) y tiene ruido de compresión: se lleva a blanco puro.
    recorte = recorte.point(lambda v: 255 if v >= 246 else v)
    print(f"horizontal: caja {caja} -> {recorte.size}")
    return recorte


def escalar_alto(im: Image.Image, alto: int) -> Image.Image:
    ancho = round(im.width * alto / im.height)
    return im.resize((ancho, alto), Image.LANCZOS)


def escalar_ancho(im: Image.Image, ancho: int) -> Image.Image:
    alto = round(im.height * ancho / im.width)
    return im.resize((ancho, alto), Image.LANCZOS)


def png_optimo(im: Image.Image, limite: int) -> bytes:
    """PNG sin pérdida si cabe; si no, con paleta de 256 colores (conserva la transparencia)."""
    salida = io.BytesIO()
    im.save(salida, "PNG", optimize=True)
    if salida.tell() <= limite:
        return salida.getvalue()
    for colores in (256, 192, 128, 96, 64):
        if im.mode == "RGBA":
            cuant = im.quantize(colors=colores, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.FLOYDSTEINBERG)
        else:
            cuant = im.quantize(colors=colores, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.FLOYDSTEINBERG)
        salida = io.BytesIO()
        cuant.save(salida, "PNG", optimize=True)
        if salida.tell() <= limite:
            return salida.getvalue()
    return salida.getvalue()


def webp_optimo(im: Image.Image, limite: int) -> bytes:
    for calidad in (90, 85, 80, 75, 70, 60, 50, 40):
        salida = io.BytesIO()
        # alpha_quality 60: el borde transparente del logo cuadrado no necesita más y ahorra ~25 %.
        im.save(salida, "WEBP", quality=calidad, alpha_quality=60, method=6)
        if salida.tell() <= limite:
            return salida.getvalue()
    return salida.getvalue()


def jpeg_optimo(im: Image.Image, limite: int) -> bytes:
    for calidad in (90, 85, 80, 75, 70):
        salida = io.BytesIO()
        im.save(salida, "JPEG", quality=calidad, optimize=True, progressive=True)
        if salida.tell() <= limite:
            return salida.getvalue()
    return salida.getvalue()


def sobre_blanco(im: Image.Image) -> Image.Image:
    fondo = Image.new("RGBA", im.size, (255, 255, 255, 255))
    fondo.alpha_composite(im.convert("RGBA"))
    return fondo.convert("RGB")


def cabeza_toro() -> Image.Image:
    im = Image.open(CUADRADO).convert("RGBA")
    recorte = im.crop(CAJA_TORO)
    lado = max(recorte.size)
    cuadro = Image.new("RGBA", (lado, lado), (0, 0, 0, 0))
    cuadro.alpha_composite(recorte, ((lado - recorte.width) // 2, (lado - recorte.height) // 2))
    return cuadro


def icono(base: Image.Image, lado: int, opaco: bool, margen: float = 0.0) -> Image.Image:
    interior = round(lado * (1 - 2 * margen))
    pequeño = base.resize((interior, interior), Image.LANCZOS)
    lienzo = Image.new("RGBA", (lado, lado), (255, 255, 255, 255 if opaco else 0))
    lienzo.alpha_composite(pequeño, ((lado - interior) // 2, (lado - interior) // 2))
    return lienzo.convert("RGB") if opaco else lienzo


def escribir(nombre: str, datos: bytes, carpeta: Path = DESTINO) -> None:
    ruta = carpeta / nombre
    ruta.write_bytes(datos)
    limite = LIMITES.get(nombre)
    estado = "" if limite is None or len(datos) <= limite else f"  SUPERA {limite // KB} KB"
    print(f"{ruta.relative_to(RAIZ)}: {len(datos) / KB:.1f} KB{estado}")
    if estado:
        sys.exit(1)


def main() -> None:
    DESTINO.mkdir(parents=True, exist_ok=True)

    horizontal = escalar_alto(recortar_horizontal(), 112)
    escribir("logo-horizontal.webp", webp_optimo(horizontal, LIMITES["logo-horizontal.webp"]))
    escribir("logo-horizontal.png", png_optimo(horizontal, LIMITES["logo-horizontal.png"]))
    print(f"logo-horizontal: {horizontal.size}")

    cuadrado = escalar_ancho(Image.open(CUADRADO).convert("RGBA"), 320)
    # CA-P10-11: la portada (horizontal + cuadrado en WebP) no debe pasar de 40 KB en imágenes de marca.
    escribir("logo-cuadrado.webp", webp_optimo(cuadrado, 40 * KB - len(webp_optimo(horizontal, LIMITES["logo-horizontal.webp"]))))
    escribir("logo-cuadrado.png", png_optimo(cuadrado, LIMITES["logo-cuadrado.png"]))
    print(f"logo-cuadrado: {cuadrado.size}")

    og = Image.new("RGB", (1200, 630), (255, 255, 255))
    logo_og = escalar_ancho(recortar_horizontal(), 1080)
    og.paste(logo_og, ((1200 - logo_og.width) // 2, (630 - logo_og.height) // 2))
    escribir("og-portal.jpg", jpeg_optimo(og, LIMITES["og-portal.jpg"]))

    toro = cabeza_toro()
    escribir("favicon-32.png", png_optimo(icono(toro, 32, opaco=False), LIMITES["favicon-32.png"]))
    escribir("apple-touch-icon.png", png_optimo(icono(toro, 180, opaco=True, margen=0.06), LIMITES["apple-touch-icon.png"]))
    escribir("icon-192.png", png_optimo(icono(toro, 192, opaco=True, margen=0.06), LIMITES["icon-192.png"]))
    escribir("icon-512.png", png_optimo(icono(toro, 512, opaco=True, margen=0.06), LIMITES["icon-512.png"]))

    # /favicon.ico en la raíz: los navegadores lo piden ahí aunque no esté enlazado.
    ico = io.BytesIO()
    icono(toro, 48, opaco=False).save(ico, "ICO", sizes=[(16, 16), (32, 32), (48, 48)])
    escribir("favicon.ico", ico.getvalue(), PUBLICO)


if __name__ == "__main__":
    main()
