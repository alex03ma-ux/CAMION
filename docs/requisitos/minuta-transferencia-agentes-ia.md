> Texto extraído de «CAMION_Documento_Maestro_Agentes_IA.docx» (minuta de transferencia, corte 26/10/2026). Las tablas aparecen linealizadas.


RED DE VENTA
FINANCIAMOS TU CAMIÓN
Documento maestro de continuidad, minuta funcional y plan de desarrollo mediante agentes de IA
Versión de transferencia 1.0  |  Corte documental: 26 de octubre de 2026
Base funcional: especificación v3.0 del proyecto; estado técnico: último avance confirmado en esta conversación.
PROPÓSITO: entregar a un agente desarrollador la visión del producto, las reglas de negocio, el estado real del repositorio y las condiciones para continuar sin destruir trabajo ni inventar funcionalidades.
1. Resumen ejecutivo
Red de Venta – Financiamos Tu Camión es una plataforma web orientada a publicar, promocionar y comercializar vehículos, con énfasis en camiones, y conectar compradores, asesores y administración. La versión funcional objetivo es 3.0. Debe integrar catálogo público, inventario, publicaciones, leads, búsquedas, créditos, gestión comercial, usuarios, planes, contenido, favoritos, notificaciones, reportes, seguridad y parametrización.
Situación comprobada: existe una base técnica React + ASP.NET Core + MariaDB; se probaron registro de comprador, inicio de sesión, emisión de JWT y consulta del perfil, primero mediante API y luego desde la interfaz web. El resto del alcance funcional está documentado, pero NO debe asumirse implementado.
2. Ubicación de los activos y continuidad
Activo
Ubicación / referencia
Estado conocido
Repositorio principal
https://github.com/alex03ma-ux/CAMION
Rama main; push confirmado
Último commit confirmado
eb1a733
Registro e inicio de sesión de compradores en React
Código local
Carpeta CAMION dentro de “proyecto venta de camiones” en Documentos (Windows)
La ruta absoluta debe verificarse en el equipo
Frontend
CAMION/frontend; React 19, Vite 7, TypeScript
Pantallas de acceso y perfil probadas
Backend
CAMION/backend/Camion.Api; ASP.NET Core / .NET 8
API de autenticación probada
Base de datos
MariaDB en XAMPP, base camion_ventas, puerto 3306
39 tablas verificadas; no implica validar todos los registros
Documentación existente
CAMION/docs y documentos funcionales v3.0
Revisar contra el código antes de implementar
Respaldo SQL
C:\Respaldo_CAMION\SQL\camion_ventas_recuperada.sql
Copia exportada en recuperación del 13/10/2026
Respaldos frontend
CAMION/frontend/respaldos/
Locales, excluidos del commit; conservar
No incluir en el repositorio secretos JWT, archivos de configuración con credenciales, bases de datos completas con datos personales ni carpetas de respaldo sin una política explícita.
3. Estado técnico: lo que sí está comprobado
MariaDB recuperada y disponible; la ruta /health/database respondió {status: ok, database: camion_ventas, tables: 39}.
Backend en desarrollo: http://localhost:5080. Frontend Vite: http://localhost:5173. Proxy de Vite configurado para /api y /health.
Rutas verificadas: POST /api/auth/registro/comprador; POST /api/auth/login; GET /api/auth/me con Bearer JWT.
Registro de comprador de prueba desde web; posterior inicio de sesión y lectura de nombre, correo, teléfono y rol COMPRADOR.
UI actual: pantalla de inicio de sesión, alternancia a Crear cuenta, formulario con nombre/correo/teléfono/contraseña, perfil y cierre de sesión.
Git push confirmado a main en commit eb1a733. Último git status: main sincronizado; frontend/respaldos/ sin seguimiento.
Limitaciones pendientes: no se ha probado formalmente correo duplicado, contraseña incorrecta, expiración de token, recuperación de contraseña, permisos de todos los roles ni despliegue productivo. La sesión actual no persiste después de recargar la página. No se ha verificado en esta entrega el contenido más reciente del repositorio remoto.
4. Visión de producto y roles
Actor
Responsabilidad principal
Administrador
Aprobar asesores; administrar usuarios, roles, planes, catálogos, contenidos, destacados, reportes y excepciones.
Asesor
Registrar vehículos propios, gestionar publicaciones, atender contactos/leads, búsquedas y operaciones comerciales autorizadas.
Cliente / comprador
Consultar catálogo sin autenticarse, buscar, contactar, solicitar crédito, registrar búsquedas y gestionar favoritos según permisos.
Sistema
Ejecutar vencimientos, notificaciones, validaciones, asignaciones y auditoría.
Distinción esencial: el cliente visitante puede consultar publicaciones y contactar desde una publicación sin crear cuenta. La cuenta de comprador habilita funcionalidades adicionales; no convertir el registro en requisito para navegar el catálogo público.
5. Minuta funcional v3.0: módulos previstos
Código
Módulo
Resultado esperado
M01
Portal público
Inicio, vehículos recientes/destacados, categorías carros/camiones, filtros y detalle.
M02
Vehículos
Inventario, datos técnicos, fotografías, precios, kilometraje, edición, inactivación, venta.
M03
Publicaciones
Publicar, destacar, renovar, controlar vigencia, visibilidad y cupos.
M04
Leads
Contacto, captura, asignación, seguimiento y cierre de oportunidades.
M05
Búsquedas
Registro y seguimiento de necesidades de compra; coincidencias.
M06
Créditos
Solicitud mediante formulario, gestión, seguimiento y comunicación.
M07
Administración
Usuarios, aprobación de asesores, roles, permisos y planes.
M08
Contenido y marketing
Banners, contenidos y promociones comerciales.
M09
Favoritos
Guardar y consultar vehículos de interés.
M10
Seguridad
Autenticación, autorización, auditoría y controles de acceso.
M11
Gestión comercial
Negociación, ventas, historial y reversión auditada.
M12
Notificaciones
Alertas de leads, créditos, vencimientos y cambios de estado.
M13
Reportes
Indicadores de inventario, contactos, publicaciones, ventas y créditos.
M14
Parametrización
Catálogos y valores generales administrados centralmente.
Trazabilidad documental v3.0: HU-001 a HU-012 Portal; HU-013 a HU-022 Vehículos; HU-023 a HU-028 Publicaciones; HU-029 a HU-033 Leads; HU-034 a HU-038 Búsquedas; HU-039 a HU-042 Créditos; HU-043 a HU-051 Administración; HU-052 a HU-055 Marketing; HU-056 a HU-058 Favoritos; HU-059 a HU-063 Seguridad; HU-064 a HU-069 Gestión comercial; HU-070 a HU-074 Notificaciones; HU-075 a HU-080 Reportes; HU-081 a HU-087 Parametrización. La matriz consolidada consultada contiene un extracto parcial: el agente debe localizar las HU detalladas en los documentos originales, no inventar criterios de aceptación.
6. Reglas de negocio obligatorias y flujos
Vehículos: placa única; precio obligatorio; kilometraje positivo según regla funcional. Fotografías: máximo inicial de 10 por vehículo, sujeto a plan y almacenamiento; confirmar detalle vigente de RN-VE-001.
Publicación: prohibida sin plan activo y cupo disponible; vigencia base de 30 días; solo el asesor propietario puede modificar sus vehículos/publicaciones, salvo facultades administrativas.
Visibilidad: un vehículo vendido se oculta del catálogo público, pero se conserva el historial comercial.
Venta: registrar fecha, precio final, origen y observaciones. Reversión por asesor propietario con motivo y auditoría; reactivación requiere confirmación y cupo disponible.
Leads: contacto desde publicación activa crea oportunidad vinculada a publicación, vehículo y asesor; no exige autenticación del visitante. Solicitudes generales se asignan al administrador. Contactos de publicación al asesor activo; si no está disponible, al administrador.
Crédito: la opción “Quiero un crédito” debe abrir primero un formulario; WhatsApp se ofrece después, sin reemplazar el registro de solicitud.
Roles: asesor sujeto a aprobación administrativa; proteger API y frontend según permisos reales.
Estados: usuarios Pendiente/Aprobado/Rechazado/Bloqueado; vehículos Activo/Inactivo/Vendido; publicaciones Publicada/Vencida; leads Abierto/Cerrado; créditos Registrado/Asignado/Gestionado/Cerrado; suscripciones Activa/Suspendida/Reactivada/Vencida. Confirmar transiciones exactas en las reglas originales.
7. Procesos operativos de extremo a extremo
Proceso
Secuencia objetivo
FN-01 Venta
Asesor registra vehículo → valida datos y plan → publica → visitante consulta/contacta → se crea y asigna lead → asesor gestiona → registra venta → se oculta del catálogo y conserva historial.
FN-02 Búsqueda
Visitante define criterios → registra búsqueda cuando aplique → sistema compara opciones → asesor/admin da seguimiento → se registra resultado.
FN-03 Crédito
Interesado completa formulario → sistema registra solicitud → asignación al responsable → evaluación/seguimiento → notificaciones → cierre; WhatsApp posterior opcional.
8. Arquitectura objetivo y límites de implementación
Mantener el stack existente mientras no exista una decisión formal de migración: React + TypeScript + Vite; API ASP.NET Core .NET 8; MariaDB/MySQL. Mantener una separación explícita entre componentes de interfaz, servicios API, lógica de negocio, persistencia y pruebas. Usar migraciones versionadas y contratos tipados. Las integraciones con correo, WhatsApp Business, pasarela de pagos, Analytics y redes sociales están contempladas como posibilidades, NO como integraciones contratadas o configuradas.
No inferir que 39 tablas significan 39 funcionalidades terminadas. Antes de diseñar tablas nuevas, inspeccionar modelos, DbContext, migraciones, esquema SQL y relaciones existentes.
9. Plan de ejecución por agentes de IA
Agente
Responsabilidad / entregable
Orquestador / Product Owner IA
Leer documentos, priorizar backlog, dividir tareas, resolver dependencias, reportar avances y pedir aprobación ante ambigüedad de negocio.
Analista funcional
Convertir HU y RN en criterios de aceptación y casos positivos/negativos; preservar trazabilidad.
Arquitecto / backend
Revisar modelos y endpoints; implementar reglas, permisos, persistencia y pruebas de integración.
Frontend / UX
Construir pantallas responsivas y accesibles; consumir API existente; gestionar estados y errores.
QA automatizado
Pruebas unitarias, API, flujos E2E, seguridad básica, regresión y reporte reproducible.
DevOps / seguridad
CI, migraciones seguras, secretos, revisión de dependencias, despliegues y rollback.
Documentador
Actualizar README, manual técnico, bitácora de decisiones y manual de usuario.
10. Instrucciones obligatorias para el agente autónomo
Antes de actuar: clonar/actualizar repositorio, comprobar rama, git status, versiones, pruebas y documentación; informar si hay cambios sin guardar.
Leer Documento Maestro v3.0, reglas de negocio, matriz HU y documentación del repositorio; registrar contradicciones y proponer resolución, sin asumir.
Trabajar en rama feature/*; commits pequeños vinculados a HU/RN; nunca hacer push directo a main sin revisión humana.
Antes de alterar esquema: respaldo verificable, migración reversible, revisión de datos existentes y prueba en entorno aislado. Nunca ejecutar DROP/TRUNCATE, sobrescribir respaldos o borrar carpetas de recuperación sin autorización expresa.
No leer, imprimir, copiar ni publicar secretos; usar user-secrets o variables de entorno. No publicar contraseñas, JWT ni datos personales en logs o capturas.
Cada cambio debe incluir criterios de aceptación, validaciones, pruebas automatizadas, instrucciones de ejecución y evidencia de resultados.
No afirmar que una prueba pasó si no fue ejecutada. Distinguir IMPLEMENTADO / PROBADO / DOCUMENTADO / PROPUESTO / BLOQUEADO.
Si falta definición de negocio, detener solo esa decisión y presentar alternativas con impacto; continuar en tareas independientes seguras.
Antes de cerrar una tarea: ejecutar build frontend, pruebas backend y E2E pertinentes, revisar diff y seguridad, actualizar documentación, abrir PR para aprobación.
11. Backlog recomendado para iniciar automatización
Orden
Trabajo
Criterio de salida
P0
Auditoría inicial de repositorio y requisitos
Mapa real de endpoints, tablas, HU y cobertura; diferencias documentadas.
P1
Robustecer autenticación
Duplicados, errores HTTP claros, contraseñas inválidas, permisos, expiración, pruebas y UX.
P2
Seguridad de usuarios y aprobación de asesores
Roles y flujos aprobatorios protegidos en API; auditoría.
P3
Catálogo público de vehículos
Listado, detalle, filtros, búsqueda y visibilidad de estados.
P4
Inventario y publicaciones
CRUD seguro, imágenes, plan/cupo, vigencia y propietario.
P5
Leads y contactos
Captura anónima, asignación y seguimiento trazable.
P6
Créditos y búsquedas
Formularios, estados, asignaciones, notificaciones y seguimiento.
P7
Ventas, planes y administración
Historial, reversión, parametrización, panel administrativo.
P8
Reportes, marketing e integraciones
Indicadores e integraciones aprobadas; pruebas y operación.
12. Criterios de aceptación de una entrega
Funcionalidad alineada con HU y RN citadas; sin cambios silenciosos al alcance.
Control de permisos probado tanto en backend como en interfaz.
Validaciones y errores comprensibles; casos de éxito, duplicados y fallo cubiertos.
No se pierden registros existentes; migración y rollback documentados.
Build y pruebas relevantes ejecutados y resultados registrados.
Interfaz utilizable en móvil y escritorio; accesibilidad básica verificada.
PR revisable con descripción, archivos modificados, riesgos, evidencia y próximos pasos.
13. Primer encargo listo para entregar a un agente
OBJETIVO: continuar el proyecto CAMION desde commit confirmado eb1a733, sin reescribirlo. 1) Inspecciona el repositorio y verifica el HEAD actual; 2) revisa el módulo de autenticación y los contratos reales; 3) reproduce el registro de correo duplicado sin exponer credenciales; 4) mejora los mensajes de error del frontend según los códigos y respuestas efectivas del backend; 5) añade pruebas para registro exitoso, duplicado, validaciones, login incorrecto y /me sin token; 6) ejecuta compilación y pruebas; 7) presenta PR y reporte. No modifiques otras bases de datos ni My English Adventure. No hagas merge ni despliegues sin autorización.
14. Fuentes y control de versiones
Fuentes funcionales consultadas: “Documento Maestro de Requisitos Funcionales 3.0.docx”; “Documento_Maestro_V3_Completo.docx”; “Matriz_Funcional_Consolidada_V3.docx” (extracto parcial); “Reglas_de_Negocio financiams tu camion.pdf” (v1.0); “Financiamos_Tu_Camion_Version_2.0(1).docx”; “HU-D_HU029_HU033_Leads_COMPLETO.docx”. Fuentes técnicas: resultados compartidos de pruebas locales y Git en la conversación. En caso de discrepancia, comparar la línea base v3.0 con decisiones posteriores confirmadas y registrar la resolución.
Advertencia: este documento es una minuta de transferencia y ejecución; no reemplaza las 87 historias de usuario completas ni constituye una auditoría directa del código actual. El primer agente debe validar el estado real antes de comenzar cambios.
