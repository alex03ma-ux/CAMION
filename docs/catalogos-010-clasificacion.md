# Script 010: clasificación de modelos por categoría (D-32)

La clasificación inicial cubre las 393 líneas que había el 2026-10-28. Criterios:

- **Marcas de una sola categoría:**
  - Maquinaria: Bobcat, Case, Caterpillar, Develon, Doosan, Hyster, JCB, John Deere, Komatsu, New Holland y Yale.
  - Camión: Dongfeng, Freightliner, Fuso, Hino, International, Isuzu, Kenworth, Mack, Scania y Mercedes-Benz.
  - Carro: Kia, Mazda, Nissan, Suzuki, Renault y Ford.
  - Carrocería: Randon.
- **Marcas mixtas, clasificadas modelo a modelo:** Chevrolet, Hyundai, Toyota, Volkswagen, Volvo, Foton y JAC.

## Dudosos (PROPUESTO): revísalos

- Chevrolet Colorado → **Carro** (camioneta pickup: se clasifica como Carro)
- Chevrolet Montana → **Carro** (camioneta pickup: se clasifica como Carro)
- Chevrolet N400 → **Carro** (furgoneta o van: se clasifica como Carro (podría ser Camión ligero))
- Chevrolet Silverado → **Carro** (camioneta pickup: se clasifica como Carro)
- Ford Maverick → **Carro** (camioneta pickup: se clasifica como Carro)
- Ford Ranger → **Carro** (camioneta pickup: se clasifica como Carro)
- Foton Minitruck → **Camión** (minicamión: se clasifica como Camión)
- Foton Toano → **Carro** (furgoneta o van: se clasifica como Carro (podría ser Camión ligero))
- Foton Tunland G7 → **Carro** (camioneta pickup: se clasifica como Carro)
- Foton Tunland G9 → **Carro** (camioneta pickup: se clasifica como Carro)
- Foton Tunland V7 → **Carro** (camioneta pickup: se clasifica como Carro)
- Foton Tunland V9 → **Carro** (camioneta pickup: se clasifica como Carro)
- Foton Wonder → **Camión** (minicamión: se clasifica como Camión)
- Hyundai Staria → **Carro** (furgoneta o van: se clasifica como Carro (podría ser Camión ligero))
- JAC 1040 → **Camión** (camión ligero JAC: se clasifica como Camión)
- JAC 1063 → **Camión** (camión ligero JAC: se clasifica como Camión)
- JAC Gallardo → **Camión** (camión ligero JAC: se clasifica como Camión)
- JAC T8 → **Carro** (camioneta pickup: se clasifica como Carro)
- JAC X200 → **Camión** (camión ligero JAC: se clasifica como Camión)
- John Deere 5045D → **Maquinaria** (tractor agrícola: se clasifica como Maquinaria)
- John Deere 5075E → **Maquinaria** (tractor agrícola: se clasifica como Maquinaria)
- John Deere 5090E → **Maquinaria** (tractor agrícola: se clasifica como Maquinaria)
- John Deere 6110 → **Maquinaria** (tractor agrícola: se clasifica como Maquinaria)
- John Deere 8270R → **Maquinaria** (tractor agrícola: se clasifica como Maquinaria)
- Kia Carnival → **Carro** (furgoneta o van: se clasifica como Carro (podría ser Camión ligero))
- Kia Tasman → **Carro** (camioneta pickup: se clasifica como Carro)
- Mazda BT-50 → **Carro** (camioneta pickup: se clasifica como Carro)
- New Holland T6 → **Maquinaria** (tractor agrícola: se clasifica como Maquinaria)
- New Holland T7 → **Maquinaria** (tractor agrícola: se clasifica como Maquinaria)
- New Holland T8 → **Maquinaria** (tractor agrícola: se clasifica como Maquinaria)
- New Holland TL5 → **Maquinaria** (tractor agrícola: se clasifica como Maquinaria)
- New Holland TT4 → **Maquinaria** (tractor agrícola: se clasifica como Maquinaria)
- New Holland TT4030 → **Maquinaria** (tractor agrícola: se clasifica como Maquinaria)
- Nissan Frontier → **Carro** (camioneta pickup: se clasifica como Carro)
- Randon Basculante → **Carrocería** (Randon publica tipos de remolque/carrocería; se clasifican como Carrocería)
- Randon Base de contenedor → **Carrocería** (Randon publica tipos de remolque/carrocería; se clasifican como Carrocería)
- Randon Cañero → **Carrocería** (Randon publica tipos de remolque/carrocería; se clasifican como Carrocería)
- Randon Carga Todo → **Carrocería** (Randon publica tipos de remolque/carrocería; se clasifican como Carrocería)
- Randon Forestal → **Carrocería** (Randon publica tipos de remolque/carrocería; se clasifican como Carrocería)
- Randon Frigorífico → **Carrocería** (Randon publica tipos de remolque/carrocería; se clasifican como Carrocería)
- Randon Furgón → **Carrocería** (Randon publica tipos de remolque/carrocería; se clasifican como Carrocería)
- Randon Furgón carga seca → **Carrocería** (Randon publica tipos de remolque/carrocería; se clasifican como Carrocería)
- Randon Granelero → **Carrocería** (Randon publica tipos de remolque/carrocería; se clasifican como Carrocería)
- Randon Sider → **Carrocería** (Randon publica tipos de remolque/carrocería; se clasifican como Carrocería)
- Randon Silo → **Carrocería** (Randon publica tipos de remolque/carrocería; se clasifican como Carrocería)
- Randon Tanque → **Carrocería** (Randon publica tipos de remolque/carrocería; se clasifican como Carrocería)
- Randon Volco basculante → **Carrocería** (Randon publica tipos de remolque/carrocería; se clasifican como Carrocería)
- Randon Volquete → **Carrocería** (Randon publica tipos de remolque/carrocería; se clasifican como Carrocería)
- Renault Alaskan → **Carro** (camioneta pickup: se clasifica como Carro)
- Renault Kangoo → **Carro** (furgoneta o van: se clasifica como Carro (podría ser Camión ligero))
- Renault Master → **Carro** (furgoneta o van: se clasifica como Carro (podría ser Camión ligero))
- Renault Oroch → **Carro** (camioneta pickup: se clasifica como Carro)
- Renault Trafic → **Carro** (furgoneta o van: se clasifica como Carro (podría ser Camión ligero))
- Toyota Hilux → **Carro** (camioneta pickup: se clasifica como Carro)
- Volkswagen Amarok → **Carro** (camioneta pickup: se clasifica como Carro)
- Volkswagen Saveiro → **Carro** (camioneta pickup: se clasifica como Carro)

## Vehículos de prueba cuyo modelo no corresponde a su categoría

- Isuzu ELF 100: es **Camión**, pero lo usa un vehículo de la categoría Camión, Maquinaria. Al editarlo, la API pedirá un modelo de su categoría.
- Kia Picanto: es **Carro**, pero lo usa un vehículo de la categoría Camión. Al editarlo, la API pedirá un modelo de su categoría.
- Mazda CX-5: es **Carro**, pero lo usa un vehículo de la categoría Camión. Al editarlo, la API pedirá un modelo de su categoría.

## Todos los modelos por categoría

### Carro (113)

- **Chevrolet:** Captiva, Colorado, Montana, N400, Onix, Sail, Silverado, Spark, Tracker
- **Ford:** Bronco Sport, Escape, Explorer, Maverick, Ranger, Territory
- **Foton:** Toano, Tunland G7, Tunland G9, Tunland V7, Tunland V9
- **Hyundai:** Accent, Creta, Grand i10, HB20, i10, Kona, Palisade, Santa Fe, Staria, Tucson, Venue
- **JAC:** JS2, JS3, T8
- **Kia:** Carnival, EV3, EV5, EV9, K3, K4, Niro, Picanto, Rio, Seltos, Soluto, Sonet, Sorento, Sportage, Stonic, Tasman
- **Mazda:** 2, 3, BT-50, CX-3, CX-30, CX-5, CX-50, CX-60, CX-90, MX-5
- **Nissan:** Frontier, Kicks, March, Pathfinder, Qashqai, Sentra, Versa, X-Trail
- **Renault:** Alaskan, Arkana, Boreal, Captur, Duster, Kangoo, Kardian, Koleos, Kwid, Logan, Master, Niagara, Oroch, Sandero, Stepway, Trafic
- **Suzuki:** Alto, Baleno, Ertiga, Jimny, S-Presso, Swift, Vitara, XL7
- **Toyota:** bZ4X, Corolla, Fortuner, Hilux, Land Cruiser 300, Prado, RAV4, Yaris
- **Volkswagen:** Amarok, Gol, Jetta, Nivus, Polo, Polo Track, Saveiro, T-Cross, Taos, Tera, Tiguan, Virtus, Voyage

### Camión (96)

- **Chevrolet:** FRR, FTR, FVR, FVZ, NHR, NKR, NLR, NPR, NPS, NQR, NRR
- **Dongfeng:** Captain, Captain T, DHR, DKR, DPR, DQR, DRR, Duolika
- **Foton:** Auman, Auman EST, Aumark, BJ1069, FHR, FKR, FPR, FQR, FRR, Galaxy, Miler, Minitruck, Wonder
- **Freightliner:** 114SD, Cascadia, Columbia, M2 106
- **Fuso:** Canter, FA, FI, FJ
- **Hino:** Dutro, Dutro 300, Dutro City, FC, GH, Serie 300, Serie 500, SG1A
- **Hyundai:** HD65, HD78, Mighty, Xcient
- **International:** 4300, 4400, 7400, HV, HX, LT, MV, ProStar
- **Isuzu:** ELF 100, ELF 300, ELF 600, Forward
- **JAC:** 1040, 1063, Gallardo, JHR, JKR, JKRD, JPR, JQR, JRR, X200
- **Kenworth:** T370, T680, T800, T880
- **Mack:** Anthem, Granite
- **Mercedes-Benz:** Accelo, Actros, Arocs, Atego, Axor
- **Scania:** Serie G, Serie P, Serie R, Serie S, XT
- **Volkswagen:** Constellation, Delivery
- **Volvo:** FH, FM, FMX, VM

### Carrocería (14)

- **Randon:** Basculante, Base de contenedor, Cañero, Carga Todo, Forestal, Frigorífico, Furgón, Furgón carga seca, Granelero, Sider, Silo, Tanque, Volco basculante, Volquete

### Maquinaria (170)

- **Bobcat:** B760, E26, E35, E35z, E50z, S450, S510, S530, S570, S590, S650, S70, S770, T590, T770, T870
- **Case:** 1150L, 570SV, 580N, 580SV, 590SN, 590SV, 621G, 721G, 845C, 851FX, CX130, CX220, CX250, CX350, SR175, SR175B, SV185B
- **Caterpillar:** 120, 140K, 262D, 301, 305, 307, 313, 320, 336, 416, 416F, 420, 420F, 426, 938, 950, D4, D6, D8
- **Develon:** DL200, DL250, DL320, DL420, DL580, DX140W, DX225LC, DX27Z, DX35Z, DX360LC, DX490LC, DX530LC, DX55R
- **Doosan:** DX140, DX225
- **Hyster:** E30-40XN, H135-155FT, H190-280XD, H30-40A, H40-70A, H400-450XD, H50FT, H80-120FT, J30-40XN, J30-40XNT, W45ZHD
- **Hyundai:** H940C, HL757, HL960A, HX140, HX17AZ, HX210, HX350, R220
- **JCB:** 155, 19C-1, 1CX, 215, 270, 35Z-1, 3CX, 417, 427, 437, 4CX, 531-70, 542-70, 560-80, 85Z-2, JS200
- **John Deere:** 200G, 210P, 310L, 310P, 318P, 320P, 325G, 350P, 410L, 450P, 5045D, 5075E, 5090E, 544P, 6110, 624P, 650P, 8270R, 850P
- **Komatsu:** D155, D61, D65, GD675, HD465, PC130, PC200, PC210, PC240, PC290, PC35MR, PC360, PC490, WA200, WA320, WA380, WB93R, WB97
- **New Holland:** B110D, B95, B95D, D140B, E145D, E215D, E385D, L318, RG140B, T6, T7, T8, TL5, TT4, TT4030, W130D, W170D
- **Toyota:** 8FGCU25
- **Volvo:** EC210, L60
- **Yale:** ERC030-040VA, ERP030-040VF, ERP030-040VT, GDP40-50UX, GLP050, GP080-120VX, GP15-35MX, GP20-35UX, GP40-70N, MP20UX, MR14-25
