> Texto extraído de «Documento Maestro de Requisitos Funcionales 3.0.docx» (línea base funcional). Las tablas aparecen linealizadas: cada fila = Módulo, Submódulo, Funcionalidad, Actor, RN, HU.


DOCUMENTO MAESTRO DE REQUISITOS FUNCIONALES Y REGLAS DE NEGOCIO VERSIÓN 3.0
Aplicación: Financiamos Tu Camión
1. Información General del Proyecto
1.1 Nombre del Proyecto
Red de Venta – Financiamos Tu Camión
1.2 Aplicación
Financiamos Tu Camión
1.3 Versión
3.0
1.4 Objetivo General
Implementar una plataforma web que permita la administración, publicación, promoción y comercialización de vehículos, facilitando la interacción entre asesores, administradores y clientes potenciales.
1.5 Objetivos Específicos
Gestionar vehículos disponibles para venta.
Publicar vehículos en el portal web.
Administrar clientes potenciales (Leads).
Gestionar solicitudes de crédito.
Administrar planes comerciales.
Administrar contenido de marketing.
Gestionar notificaciones.
Generar reportes e indicadores.
Administrar parámetros generales de la plataforma.
2. Objetivo del Documento
Definir la operación funcional, reglas de negocio, actores, procesos, estados, validaciones y estructura funcional de la plataforma Financiamos Tu Camión.
3. Alcance
La solución cubrirá los siguientes procesos:
Portal Web
Gestión de Vehículos
Gestión de Publicaciones
Gestión de Leads
Gestión de Búsquedas
Gestión de Créditos
Administración de Usuarios
Gestión de Planes
Contenido y Marketing
Favoritos
Seguridad
Gestión Comercial Avanzada
Notificaciones
Reportes e Indicadores
Parametrización
4. Actores y Roles del Sistema
4.1 Administrador
Responsable de la administración integral de la plataforma.
Funciones:
Aprobar usuarios.
Gestionar planes.
Gestionar contenido.
Administrar parámetros.
Consultar reportes.
Gestionar publicaciones destacadas.
Administrar usuarios.
4.2 Asesor
Responsable de la gestión comercial de vehículos.
Funciones:
Registrar vehículos.
Gestionar publicaciones.
Atender clientes.
Gestionar leads.
Consultar búsquedas.
Administrar inventario propio.
4.3 Cliente
Usuario visitante del portal.
Funciones:
Consultar publicaciones.
Buscar vehículos.
Contactar asesores.
Registrar búsquedas.
Solicitar crédito.
Administrar favoritos.
4.4 Sistema
Responsable de procesos automáticos.
Funciones:
Enviar notificaciones.
Controlar vencimientos.
Registrar auditoría.
Validar reglas.
Gestionar estados.
5. Mapa de Procesos
5.1 Procesos Estratégicos
Administración General
Gestión de Usuarios
Gestión de Roles
Gestión de Permisos
Gestión de Planes
Gestión de Parámetros
Gestión de Contenido
Gestión Comercial
Publicaciones
Vehículos Destacados
Leads
Créditos
Marketing
5.2 Procesos Misionales
Gestión de Vehículos
Registro
Actualización
Inventario
Publicación
Venta
Gestión de Búsquedas
Registro
Seguimiento
Relación de Vehículos
Cierre
Gestión de Créditos
Solicitud
Asignación
Gestión
Cierre
Gestión Comercial
Atención de Leads
Negociación
Venta
5.3 Procesos de Apoyo
Seguridad
Autenticación
Recuperación de Contraseña
Gestión de Sesiones
Notificaciones
Correo Electrónico
WhatsApp
Alertas del Sistema
Auditoría
Trazabilidad
Logs
Control de Accesos
6. Estructura Funcional del Sistema
EP-01 Portal Web HU-001 a HU-012
EP-02 Gestión de Vehículos HU-013 a HU-022
EP-03 Gestión de Publicaciones HU-023 a HU-028
EP-04 Gestión de Leads HU-029 a HU-033
EP-05 Gestión de Búsquedas HU-034 a HU-038
EP-06 Gestión de Créditos HU-039 a HU-042
EP-07 Administración HU-043 a HU-051
EP-08 Contenido y Marketing HU-052 a HU-055
EP-09 Favoritos HU-056 a HU-058
EP-10 Seguridad HU-059 a HU-063
EP-11 Gestión Comercial Avanzada HU-064 a HU-069
EP-12 Notificaciones HU-070 a HU-074
EP-13 Reportes HU-075 a HU-080
EP-14 Parametrización HU-081 a HU-087
7. Matriz Funcional Consolidada
Módulo
Submódulo
Funcionalidad
Actor
Regla de Negocio
HU
Portal Web
Home
Consultar Portal Principal
Cliente
RN-CA-001
HU-001
Portal Web
Home
Consultar Vehículos Recientes
Cliente
RN-CA-002
HU-002
Portal Web
Home
Consultar Vehículo Destacado
Cliente
RN-VE-011
HU-003
Portal Web
Buscador
Buscar Vehículo
Cliente
RN-CA-003
HU-004
Portal Web
Categorías
Consultar Categoría Carros
Cliente
RN-CA-004
HU-005
Portal Web
Categorías
Consultar Categoría Camiones
Cliente
RN-CA-005
HU-006
Portal Web
Filtros
Filtrar por Marca
Cliente
RN-CA-006
HU-007
Portal Web
Filtros
Filtrar por Modelo
Cliente
RN-CA-007
HU-008
Portal Web
Filtros
Filtrar por Presupuesto
Cliente
RN-CA-008
HU-009
Portal Web
Filtros
Filtrar por Año
Cliente
RN-CA-009
HU-010
Portal Web
Filtros
Filtrar por Kilometraje
Cliente
RN-CA-010
HU-011
Portal Web
Filtros
Limpiar Filtros
Cliente
RN-CA-011
HU-012
Vehículos
Inventario
Registrar Vehículo
Asesor/Admin
RN-VE-001
HU-013
Vehículos
Inventario
Cargar Fotografías
Asesor/Admin
RN-VE-001
HU-014
Vehículos
Inventario
Registrar Información Complementaria
Asesor/Admin
RN-VE-002
HU-015
Vehículos
Inventario
Registrar Precio de Venta
Asesor/Admin
RN-VE-003
HU-016
Vehículos
Inventario
Registrar Kilometraje
Asesor/Admin
RN-VE-005
HU-017
Vehículos
Inventario
Registrar Características Comerciales
Asesor/Admin
RN-VE-010
HU-018
Vehículos
Inventario
Editar Vehículo
Asesor/Admin
RN-VE-009
HU-019
Vehículos
Inventario
Inactivar Vehículo
Asesor/Admin
RN-VE-004
HU-020
Vehículos
Inventario
Consultar Inventario Propio
Asesor
RN-CO-009
HU-021
Vehículos
Inventario
Consultar Inventario General
Administrador
RN-AD-001
HU-022
Publicaciones
Publicaciones
Crear Publicación
Asesor
RN-CO-001
HU-023
Publicaciones
Publicaciones
Modificar Publicación
Asesor
RN-CO-002
HU-024
Publicaciones
Publicaciones
Inactivar Publicación
Asesor/Admin
RN-CO-003
HU-025
Publicaciones
Publicaciones
Renovar Publicación
Asesor/Admin
RN-SU-002
HU-026
Publicaciones
Publicaciones
Destacar Publicación
Administrador
RN-CO-004
HU-027
Publicaciones
Publicaciones
Vencimiento Automático
Sistema
RN-CO-005
HU-028
Leads
Leads
Contactar Asesor
Cliente
RN-CL-001
HU-029
Leads
Leads
Generar Lead
Sistema
RN-CL-002
HU-030
Leads
Leads
Asignar Lead
Sistema/Admin
RN-CL-003
HU-031
Leads
Leads
Gestionar Lead
Asesor
RN-CL-004
HU-032
Leads
Leads
Cerrar Lead
Asesor
RN-CL-004
HU-033
Búsquedas
Búsquedas
Registrar Búsqueda
Cliente
RN-BU-001
HU-034
Búsquedas
Búsquedas
Consultar Búsquedas
Asesor/Admin
RN-BU-001
HU-035
Búsquedas
Búsquedas
Relacionar Vehículo a Búsqueda
Asesor
RN-BU-002
HU-036
Búsquedas
Búsquedas
Gestionar Búsqueda
Asesor
RN-BU-002
HU-037
Búsquedas
Búsquedas
Cerrar Búsqueda
Asesor
RN-BU-002
HU-038
Créditos
Créditos
Solicitar Crédito
Cliente
RN-CO-006
HU-039
Créditos
Créditos
Registrar Solicitud
Sistema
RN-CO-006
HU-040
Créditos
Créditos
Asignar Solicitud
Sistema/Admin
RN-CO-007
HU-041
Créditos
Créditos
Gestionar Solicitud
Asesor/Admin
RN-CO-008
HU-042
Administración
Usuarios
Aprobar Usuario
Administrador
RN-US-001
HU-043
Administración
Usuarios
Rechazar Usuario
Administrador
RN-US-002
HU-044
Administración
Usuarios
Activar Acceso
Administrador
RN-US-003
HU-045
Administración
Usuarios
Enviar Credenciales
Sistema
RN-US-004
HU-046
Administración
Usuarios
Bloquear Usuario
Administrador
RN-US-005
HU-047
Administración
Usuarios
Consultar Usuarios
Administrador
RN-US-001
HU-048
Administración
Planes
Crear Plan
Administrador
RN-PL-001
HU-049
Administración
Planes
Modificar Plan
Administrador
RN-PL-001
HU-050
Administración
Planes
Activar/Inactivar Plan
Administrador
RN-PL-002
HU-051
Marketing
Contenido
Crear Contenido
Administrador
RN-MKT-001
HU-052
Marketing
Contenido
Editar Contenido
Administrador
RN-MKT-001
HU-053
Marketing
Contenido
Publicar Contenido
Administrador
RN-MKT-001
HU-054
Marketing
Contenido
Consultar Contenido
Cliente
RN-CA-012
HU-055
Favoritos
Favoritos
Agregar Favorito
Cliente
RN-SE-005
HU-056
Favoritos
Favoritos
Eliminar Favorito
Cliente
RN-SE-005
HU-057
Favoritos
Favoritos
Consultar Favoritos
Cliente
RN-SE-005
HU-058
Seguridad
Acceso
Iniciar Sesión
Usuario
RN-SE-001
HU-059
Seguridad
Acceso
Cierre Automático de Sesión
Sistema
RN-SE-002
HU-060
Seguridad
Auditoría
Registrar Auditoría
Sistema
RN-SE-003
HU-061
Seguridad
Datos
Proteger Datos Personales
Sistema
RN-SE-004
HU-062
Seguridad
Acceso
Recuperar Contraseña
Usuario
RN-US-006
HU-063
Gestión Comercial
Vehículos
Marcar Vehículo como Vendido
Asesor/Admin
RN-GC-001
HU-064
Gestión Comercial
Publicaciones
Consultar Mis Publicaciones
Asesor
RN-GC-002
HU-065
Gestión Comercial
Suscripciones
Consultar Mi Suscripción
Asesor
RN-SU-001
HU-066
Gestión Comercial
Suscripciones
Renovar Suscripción
Asesor/Admin
RN-SU-002
HU-067
Gestión Comercial
Suscripciones
Suspender Suscripción
Administrador
RN-SU-003
HU-068
Gestión Comercial
Suscripciones
Reactivar Suscripción
Administrador
RN-SU-004
HU-069
Notificaciones
Configuración
Configurar Notificaciones
Usuario
RN-NO-001
HU-070
Notificaciones
Leads
Notificación Nuevo Lead
Sistema
RN-NO-002
HU-071
Notificaciones
Créditos
Notificación Solicitud Crédito
Sistema
RN-NO-003
HU-072
Notificaciones
Publicaciones
Notificación Vencimiento Publicación
Sistema
RN-NO-004
HU-073
Notificaciones
Suscripciones
Notificación Vencimiento Suscripción
Sistema
RN-NO-005
HU-074
Reportes
Dashboard
Dashboard General
Administrador
RN-RE-001
HU-075
Reportes
Vehículos
Reporte de Vehículos
Administrador
RN-RE-002
HU-076
Reportes
Leads
Reporte de Leads
Administrador
RN-RE-003
HU-077
Reportes
Créditos
Reporte de Solicitudes de Crédito
Administrador
RN-RE-004
HU-078
Reportes
Exportación
Exportar Reportes
Administrador
RN-RE-005
HU-079
Reportes
Indicadores
Indicadores Comerciales
Administrador
RN-RE-006
HU-080
Parametrización
General
Administrar Parámetros Generales
Administrador
RN-PA-001
HU-081
Parametrización
Catálogos
Administrar Marcas
Administrador
RN-PA-002
HU-082
Parametrización
Catálogos
Administrar Modelos
Administrador
RN-PA-003
HU-083
Parametrización
Catálogos
Administrar Tipos de Carrocería
Administrador
RN-PA-004
HU-084
Parametrización
Corporativo
Administrar Información Corporativa
Administrador
RN-PA-005
HU-085
Parametrización
Legal
Administrar Textos Legales
Administrador
RN-PA-006
HU-086
Parametrización
Comercial
Administrar Configuración Comercial
Administrador
RN-PA-007
HU-087
8. Flujos de Negocio
FN-01 Venta de Vehículos
Registro Usuario → Aprobación → Activación → Registro Vehículo → Publicación → Lead → Negociación → Venta → Cierre
FN-02 Gestión de Búsquedas
Registro Búsqueda → Validación → Publicación → Relación Vehículo → Atención → Cierre
FN-03 Gestión de Créditos
Solicitud Crédito → Registro → Asignación → Gestión → Resultado → Cierre
FN-04 Gestión de Suscripciones
Creación Plan → Asignación → Activación → Renovación → Suspensión → Reactivación
FN-05 Gestión de Contenido
Creación → Edición → Publicación → Consulta → Archivo
9. Reglas de negocio
9.1 Gestión de Consulta y Catálogo Público
Código
Nombre
Descripción
Observaciones
RN-CA-001
Consulta Portal Principal
Permitir consulta pública de la página principal.
No requiere autenticación
RN-CA-002
Consulta Vehículos Recientes
Mostrar publicaciones activas ordenadas por fecha.
Solo vigentes
RN-CA-003
Búsqueda de Vehículos
Permitir búsquedas por criterios configurados.
Consulta pública
RN-CA-004
Consulta Categoría Carros
Consultar vehículos categoría carros.
Pública
RN-CA-005
Consulta Categoría Camiones
Consultar vehículos categoría camiones.
Pública
RN-CA-006
Filtro por Marca
Filtrar publicaciones por marca.
Resultados activos
RN-CA-007
Filtro por Modelo
Filtrar publicaciones por modelo.
Resultados activos
RN-CA-008
Filtro por Presupuesto
Filtrar por rango de precio.
Valores COP
RN-CA-009
Filtro por Año
Filtrar por año.
Consulta pública
RN-CA-010
Filtro por Kilometraje
Filtrar por kilometraje.
Consulta pública
RN-CA-011
Limpiar Filtros
Restablecer filtros.
Consulta inicial
RN-CA-012
Consulta de Contenido
Consultar contenido publicado.
Marketing
9.2 Gestión de Vehículos
Código
Nombre
Descripción
Observaciones
RN-VE-001
Fotografías del Vehículo
Permitir hasta 10 fotografías por vehículo.
Depende del plan
RN-VE-002
Placa del Vehículo
Dato opcional.
No obligatoria ni única
RN-VE-003
Precio de Venta
Precio obligatorio.
Conservar precio actual y anterior
RN-VE-004
Inactivación de Vehículo
Permite inactivar vehículos.
Conserva trazabilidad
RN-VE-005
Kilometraje
Debe registrarse kilometraje.
Valor numérico
RN-VE-006
Modelo
Debe registrarse modelo.
Catálogo
RN-VE-007
Registro de Vehículo
Todo vehículo requiere propietario de publicación.
Asesor
RN-VE-008
Estado del Vehículo
Debe tener estado activo o inactivo.
Control operativo
RN-VE-009
Trazabilidad del Vehículo
Registrar cambios realizados.
Auditoría
RN-VE-010
Características Comerciales
Permitir registrar especificaciones.
Comercial
RN-VE-011
Vehículo Destacado
Puede ser marcado como destacado.
Administrador
RN-VE-012
Prioridad de Destacados
Los destacados se muestran primero.
Portal
RN-VE-013
Publicación Vigente
Solo mostrar vehículos vigentes.
Portal
RN-VE-014
Vehículo Vendido
Permite marcar vehículo como vendido.
Oculta publicación
9.3 Usuarios y Administración
Código
Nombre
Descripción
Observaciones
RN-US-001
Aprobación de Usuarios
Todo usuario debe ser aprobado.
Administrador
RN-US-002
Rechazo de Usuarios
Permite rechazar solicitudes.
Administrador
RN-US-003
Activación de Acceso
Habilita acceso al sistema.
Posterior aprobación
RN-US-004
Envío de Credenciales
Envía credenciales iniciales.
Correo
RN-US-005
Notificación de Acceso
Genera alertas de acceso.
Seguridad
RN-US-006
Recuperación de Contraseña
Permite restablecer contraseña.
Enlace temporal
9.4 Comercial y Leads
Código
Nombre
Descripción
Observaciones
RN-CO-001
Crear Publicación
Permite publicar vehículos.
Plan activo
RN-CO-002
Modificar Publicación
Permite actualizar publicación.
Propietario
RN-CO-003
Inactivar Publicación
Permite retirar publicación.
Sin eliminar
RN-CO-004
Destacar Publicación
Promociona publicación.
Administrador
RN-CO-005
Vencimiento Automático
Vence al finalizar vigencia.
Sistema
RN-CO-006
Solicitud de Crédito
Permite registrar solicitudes.
Cliente
RN-CO-007
Asignación de Solicitudes
Asigna responsable.
Sistema/Admin
RN-CO-008
Gestión de Solicitudes
Permite gestionar solicitud.
Seguimiento
RN-CO-009
Consulta de Publicaciones Propias
El asesor consulta sus publicaciones.
Restricción por propietario
RN-CL-001
Contacto con Asesor
Permite contacto desde portal.
Cliente
RN-CL-002
Generación de Lead
Genera lead automáticamente.
Sistema
RN-CL-003
Asignación de Lead
Asigna lead al responsable.
Automática
RN-CL-004
Gestión de Lead
Seguimiento y cierre.
Comercial
9.5 Búsquedas
Código
Nombre
Descripción
Observaciones
RN-BU-001
Registro de Búsquedas
Permite registrar necesidades.
Cliente
RN-BU-002
Gestión de Búsquedas
Relacionar y cerrar búsquedas.
Asesor
9.6 Planes y Suscripciones
Código
Nombre
Descripción
Observaciones
RN-PL-001
Administración de Planes
Crear y modificar planes.
Administrador
RN-PL-002
Activación de Planes
Activar/Inactivar planes.
Administrador
RN-SU-001
Consulta de Suscripción
Consultar estado de suscripción.
Asesor
RN-SU-002
Renovación de Suscripción
Renovar por vigencia.
30 días
RN-SU-003
Suspensión de Suscripción
Suspender beneficios.
Administrador
RN-SU-004
Reactivación de Suscripción
Restablecer beneficios.
Administrador
RN-SU-005
Vigencia de Suscripción
Duración de 30 días.
Renovable
9.7 Seguridad
Código
Nombre
Descripción
Observaciones
RN-SE-001
Inicio de Sesión
Validar credenciales.
Acceso
RN-SE-002
Cierre Automático
Cerrar sesiones inactivas.
Seguridad
RN-SE-003
Auditoría
Registrar eventos relevantes.
Trazabilidad
RN-SE-004
Protección de Datos
Cumplir protección de datos.
Legal
RN-SE-005
Consulta Pública
Visitantes pueden consultar publicaciones.
Sin rol comprador
RN-SE-006
Control de Accesos
Validar permisos por rol.
Seguridad
9.8 Notificaciones
Código
Nombre
Descripción
Observaciones
RN-NO-001
Configuración de Notificaciones
Preferencias de notificación.
Usuario
RN-NO-002
Notificación Lead
Notificar nuevo lead.
Tiempo real
RN-NO-003
Notificación Crédito
Notificar solicitud crédito.
Responsable
RN-NO-004
Notificación Publicación
Notificar vencimientos.
Preventiva
RN-NO-005
Notificación Suscripción
Notificar vencimiento suscripción.
Preventiva
9.9 Reportes
Código
Nombre
Descripción
Observaciones
RN-RE-001
Dashboard General
Indicadores consolidados.
Administración
RN-RE-002
Reporte Vehículos
Estadísticas de vehículos.
Filtrable
RN-RE-003
Reporte Leads
Estadísticas de leads.
Filtrable
RN-RE-004
Reporte Créditos
Estadísticas de créditos.
Filtrable
RN-RE-005
Exportación
Exportar reportes.
Excel/PDF
RN-RE-006
Indicadores Comerciales
Métricas comerciales.
Dashboard
9.10 Parametrización
Código
Nombre
Descripción
Observaciones
RN-PA-001
Parámetros Generales
Configuración global.
Sistema
RN-PA-002
Marcas
Administrar marcas.
Catálogo
RN-PA-003
Modelos
Administrar modelos.
Catálogo
RN-PA-004
Tipos de Carrocería
Administrar carrocerías.
Catálogo
RN-PA-005
Información Corporativa
Administrar información corporativa.
Portal
RN-PA-006
Textos Legales
Administrar textos legales.
Cumplimiento
RN-PA-007
Configuración Comercial
Administrar promociones y parámetros.
Comercial

10. Historias de Usuario
Portal Web
HU-001 Consultar Portal Principal
HU-002 Consultar Vehículos Recientes
HU-003 Consultar Vehículo Destacado
HU-004 Buscar Vehículo
HU-005 Consultar Categoría Carros
HU-006 Consultar Categoría Camiones
HU-007 Filtrar por Marca
HU-008 Filtrar por Modelo
HU-009 Filtrar por Presupuesto
HU-010 Filtrar por Año
HU-011 Filtrar por Kilometraje
HU-012 Limpiar Filtros
Vehículos
HU-013 Registrar Vehículo
HU-014 Cargar Fotografías
HU-015 Registrar Información Complementaria
HU-016 Registrar Precio de Venta
HU-017 Registrar Kilometraje
HU-018 Registrar Características Comerciales
HU-019 Editar Vehículo
HU-020 Inactiva Vehículo
HU-021 Consultar Inventario Propio
HU-022 Consultar Inventario General
Publicaciones
HU-023 Crear Publicación
HU-024 Modificar Publicación
HU-025 Inactivar Publicación
HU-026 Renovar Publicación
HU-027 Destacar Publicación
HU-028 Vencimiento Automático de Publicación
Leads
HU-029 Contactar Asesor
HU-030 Generar Lead
HU-031 Asignar Lead
HU-032 Gestionar Lead
HU-033 Cerrar Lead
Búsquedas
HU-034 Registrar Búsqueda
HU-035 Consultar Búsquedas
HU-036 Relacionar Vehículo a Búsqueda
HU-037 Gestionar Búsqueda
HU-038 Cerrar Búsqueda
Créditos
HU-039 Solicitar Crédito
HU-040 Registrar Solicitud
HU-041 Asignar Solicitud
HU-042 Gestionar Solicitud
Administración
HU-043 Registrar Usuario
HU-044 Aprobar Usuario
HU-045 Rechazar Usuario
HU-046 Activar Acceso
HU-047 Enviar Credenciales
HU-048 Bloquear Usuario
HU-049 Consultar Usuarios
HU-050 Crear Plan
HU-051 Modificar Plan
HU-052 Activar/Inactivar Plan

Contenido y Marketing
HU-053 Crear Contenido
HU-054 Editar Contenido
HU-055 Publicar Contenido
HU-056 Consultar Contenido
Favoritos
HU-057 Agregar Favorito
HU-058 Eliminar Favorito
HU-059 Consultar Favoritos
Seguridad
HU-060 Iniciar Sesión
HU-061 Cierre Automático de Sesión
HU-062 Registrar Auditoría
HU-063 Proteger Datos Personales
HU-064 Recuperar Contraseña
Gestión Comercial Avanzada
HU-065 Marcar Vehículo como Vendido
HU-066 Consultar Mis Publicaciones
HU-067 Consultar Mi Suscripción
HU-068 Renovar Suscripción
HU-069 Suspender
HU-070 Reactivar Suscripción
Notificaciones
HU-071 Configurar Notificaciones
HU-072 Notificación Nuevo Lead
HU-073 Notificación Solicitud Crédito
HU-074 Notificación Vencimiento Publicación
Reportes
HU-075 Dashboard General
HU-076 Reporte de Vehículos
HU-077 Reporte de Leads
HU-078 Reporte de Solicitudes de Crédito
HU-079 Exportar Reportes
HU-080 Indicadores Comerciales
Parametrización
HU-081 Notificación Vencimiento Suscripción
HU-082 Administrar Parámetros Generales
HU-083 Administrar Marcas
HU-084 Administrar Modelos
HU-085 Administrar Tipos de Carrocería
HU-086 Administrar Información Corporativa
HU-087 Administrar Textos Legales
HU-088 Administrar Configuración Comercial
CRM Comercial (Nuevo Módulo)
HU-089 Consultar Solicitudes Comerciales 
HU-090 Dashboard Comercial

11. Validaciones del Sistema
Validación de usuarios únicos por correo electrónico.
Validación de contraseña segura.
Validación de plan activo para publicar vehículos.
Validación de vigencia de suscripción (30 días).
Validación de fotografías permitidas según plan.
Validación de precio obligatorio en publicaciones.
Validación de campos obligatorios del vehículo.
Validación de acceso por roles y permisos.
Validación de estado activo para visualizar publicaciones.
Validación de formatos de correo electrónico y teléfono.
12. Estados del Sistema
Entidad
Estado
Descripción
Usuario
Pendiente
Pendiente de aprobación
Usuario
Aprobado
Usuario habilitado
Usuario
Rechazado
Solicitud rechazada
Usuario
Bloqueado
Acceso restringido
Vehículo
Activo
Disponible para publicación
Vehículo
Inactivo
No disponible
Vehículo
Vendido
Venta realizada
Publicación
Publicada
Visible al público
Publicación
Vencida
Fuera de vigencia
Lead
Abierto
Pendiente de gestión
Lead
Cerrado
Lead finalizado
Crédito
Registrado
Solicitud creada
Crédito
Asignado
Responsable asignado
Crédito
Gestionado
En seguimiento
Suscripción
Activa
Suscripción vigente
Suscripción
Suspendida
Temporalmente inactiva
Suscripción
Vencida
Finalizó vigencia
13. Aplicación de Estados
Usuarios: Pendiente → Aprobado/Rechazado → Bloqueado
Vehículos: Activo → Inactivo → Vendido
Publicaciones: Publicada → Vencida
Leads: Abierto → Cerrado
Créditos: Registrado → Asignado → Gestionado → Cerrado
Suscripciones: Activa → Suspendida → Reactivada → Vencida
14. Integraciones
Las integraciones serán definidas durante el diseño técnico.
Posibles integraciones:
Correo electrónico
WhatsApp Business
Pasarela de pagos
Google Analytics
Redes sociales
YouTube/TikTok
15. Restricciones del Sistema
No se permitirá publicar vehículos sin plan activo.
Las publicaciones tendrán vigencia de 30 días.
Solo administradores podrán gestionar planes.
Solo administradores podrán parametrizar catálogos.
Los asesores solo podrán modificar sus propios vehículos y publicaciones.
Los visitantes no requieren autenticación para consultar publicaciones.
16. Observaciones Generales
La presente versión 3.0 constituye la línea base funcional del proyecto Financiamos Tu Camión.
Toda modificación posterior deberá gestionarse mediante control de cambios y actualización de trazabilidad.
17. Aprobaciones
Nombre
Cargo
Firma
Fecha


















