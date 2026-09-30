# Base de datos al instalar el sistema

`parking_dbContext` se generó a partir de una base SQL Server existente con EF Core Power Tools. Los modelos C# describen las tablas y permiten consultar datos, pero **no crean una base nueva ni actualizan una existente por sí solos**. Este proyecto no tiene migraciones de EF Core.

## Trasladar una instalación existente

1. Hacer una copia de seguridad de `parking_db` en el servidor actual y restaurarla en el servidor de destino. Así se conservan el esquema, los datos, los usuarios y sus hashes.
2. Configurar en el primer inicio del programa la cadena de conexión de ese equipo.
3. Si la tabla `users` está vacía, el programa abre el asistente de creación del primer administrador. Si se trasladaron usuarios, se ingresa con las credenciales existentes.

La aplicación crea las tablas auxiliares `parking_incident_reviews`, `parking_shift_closures` y `parking_monthly_fee_receipts` cuando se utilizan sus módulos, siempre que la cuenta SQL tenga permiso `CREATE TABLE`. Sus entidades y configuraciones EF permanecen en el proyecto.

## Servidor SQL completamente vacío

Instalar SQL Server no crea automáticamente `parking_db`. Antes de iniciar sesión hay que provisionar el esquema de la base, por ejemplo restaurando una copia preparada para instalaciones nuevas. El asistente de administrador necesita que ya exista la tabla `users`.

Actualmente el proyecto **no incluye scripts SQL de instalación ni migraciones**. Por tanto, el instalador todavía no prepara por sí solo un servidor SQL completamente vacío. Si se usará ese escenario, hay que implementar y probar el aprovisionamiento de la base antes de distribuir el instalador.
