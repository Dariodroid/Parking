# Licencias sin servidor

Cada licencia funciona solamente con el código del equipo para el que fue emitida. El sistema la comprueba **antes** de abrir la conexión SQL, crear el primer administrador o mostrar el login. No necesita Internet para activarse ni para seguir funcionando.

## Entregar una licencia a un cliente

1. El cliente abre Parking y pulsa **Copiar código** en la ventana de activación. Te envía esos 32 caracteres.
2. En tu laptop, abre `Parking.LicenseIssuer.exe` desde el proyecto **Tools → Parking.LicenseIssuer**. Escribe el nombre del cliente y el código recibido.
3. Elige **1, 3, 6 o 12 meses**, **Hasta una fecha** o **Sin vencimiento**. Pulsa **Generar serial** y luego **Copiar serial**.
4. Envía al cliente ese texto. Él lo pega en Parking y pulsa **Activar serial**. También puedes usar **Guardar archivo** para conservar o enviar una copia del serial.

La herramienta busca tu clave privada en `C:\Users\Dario Castillo\Documents\ParkingLicenses\parking-private.pem`. Puedes elegir otra ruta con **Buscar...**. **Nunca entregues esa clave privada ni la incluyas en el instalador o en GitHub**: solo debes enviar el serial. Guarda una copia de seguridad cifrada de la clave; sin ella no podrás emitir nuevos seriales compatibles con la aplicación.

## Qué ocurre si copian el programa

El mismo serial se rechaza en una PC con otro código. Si no hay licencia válida, el programa muestra un aviso para contactar al proveedor y no permite entrar al sistema, aunque hayan copiado la carpeta instalada y la base de datos. En otra cuenta de Windows de **la misma PC** se puede introducir el mismo serial.

Una licencia con vencimiento deja de funcionar al llegar a su fecha y hora final; puedes emitir y enviar otro serial para renovar. La opción **Sin vencimiento** no caduca. Si reinstalan Windows o cambia el identificador del equipo, tendrás que emitir una licencia nueva.

La fecha se comprueba con el reloj local. Al no haber servidor, alguien con control avanzado de la PC puede manipular ese reloj o modificar el ejecutable .NET. Este sistema evita la copia casual, pero una licencia offline no puede garantizar protección absoluta ni revocarse a distancia. Mantén privado el código fuente y distribuye solamente la publicación de `Parking.UI.Windows`, no la herramienta emisora.
