# Base de datos al instalar el sistema

`parking_dbContext` contiene las entidades y el mapeo de las tablas existentes en SQL Server. Se puede volver a generar el modelo desde la base mediante EF Core Power Tools. La aplicación no crea ni modifica el esquema SQL al arrancar.

## Trasladar una instalación existente

1. Hacer una copia de seguridad de `parking_db` en el servidor actual y restaurarla en el servidor de destino. Así se conservan el esquema, los datos, los usuarios y sus hashes.
2. Configurar en el primer inicio del programa la cadena de conexión de ese equipo.
3. Si la tabla `users` está vacía, el programa abre el asistente de creación del primer administrador. Si se trasladaron usuarios, se ingresa con las credenciales existentes.

Las tablas `parking_incident_reviews`, `parking_shift_closures` y `parking_monthly_fee_receipts` ya forman parte de la base y están mapeadas en el contexto EF junto con las demás tablas.

## Servidor SQL completamente vacío

Instalar SQL Server no crea automáticamente `parking_db`. Antes de iniciar sesión hay que preparar la estructura de la base. El asistente de administrador necesita que ya exista la tabla `users`.

El proyecto no incluye migraciones ni ejecuta una creación automática de tablas. Tener las clases C# permite trabajar con EF y generar un esquema aparte, pero los modelos por sí solos no crean la base al instalar el programa.
