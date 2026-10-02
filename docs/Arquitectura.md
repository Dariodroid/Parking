# Arquitectura del sistema de parqueadero

## Dirección de referencias

```text
Parking.Domain.Model              (sin referencias a las otras capas)
Parking.Application.Dto           (datos de entrada y salida)
Parking.Application               -> Domain.Model, Application.Dto
Parking.Infrastructure.*          -> contratos de Application o Domain.Model
Parking.UI.Windows                -> Application y Domain.Model
Parking.UI.Windows/App.xaml.cs    -> Infrastructure para registrar implementaciones
```

`App.xaml.cs` es la raíz de composición del ejecutable WPF. Por eso el proyecto UI
referencia las implementaciones de Infrastructure, aunque los ViewModels deben
recibir interfaces. Esa referencia de arranque no invierte la dependencia del
dominio: ni Domain.Model ni Application conocen UI o Infrastructure.

## Responsabilidad de cada lugar

- **Domain.Model:** entidades, reglas del contrato e interfaces de persistencia
  que trabajan con esas entidades. `MonthlyAccessPolicy` decide la modalidad de
  ingreso sin abrir SQL ni cámaras. `OperationsControlPolicy` decide incidencias,
  clasifica pagos y valida cierres sin conocer EF ni la UI. Todas sus interfaces
  están en `Interfaces`.
- **Application:** casos de uso como `EntryService`; interfaces de consultas,
  cobros y servicios externos; contratos de datos que cruzan capas. Decide el
  orden de consultas y cambios de estado y solicita la persistencia.
  `Interfaces` reúne todas las abstracciones que consumen los casos de uso o
  la UI; `Contracts` reúne tipos de datos compartidos como `CameraSource` y
  `PlateDetectionResult`. El nombre anterior `Ports` ya no se usa.
- **Infrastructure.DataAccess:** EF Core, SQL Server, repositorios y transacciones
  del primer administrador, cuotas mensuales y centro de control. Implementa
  interfaces de Domain y Application; no declara interfaces propias. Los cobros
  mensuales, las revisiones y los cierres usan entidades mapeadas por EF Core.
- **Infrastructure.ExternalServices:** captura de cámara, detección de placas,
  OCR, QR y almacenamiento de fotos y tickets.
- **UI.Windows:** vistas, ViewModels, diálogos, temas, impresión WPF y
  preferencias locales de esta instalación. Sus interfaces visuales están en
  `Interfaces` y sus implementaciones en `Services`. El dibujo de la guía sobre la
  imagen se queda en UI; las coordenadas y el resultado del reconocimiento
  llegan mediante `IPlateService` sin exponer `OpenCvSharp.Rect`.

## Recorridos principales

Una entrada comienza en `PlateReaderViewModel`, pasa a `IEntryService` y
`EntryService`, que consulta el vehículo, pide a `MonthlyAccessPolicy` la
decisión de acceso y guarda sesión y puesto mediante repositorios. La lectura
de cámara usa `ICameraService` e `IPlateService`; las implementaciones OpenCV
se eligen en `App.xaml.cs`.

Los formularios de usuarios, tipos de vehículo, puestos y clientes llaman a
servicios de Application. Estos coordinan las escrituras y consultas mediante
los repositorios de Domain; los ViewModels no reciben esos repositorios.
Caja, tablero y reportes también consultan mediante servicios de Application;
las consultas EF y la persistencia de posiciones permanecen en DataAccess.

El primer administrador, las cuotas mensuales y el centro de control se
solicitan desde la UI por interfaces de Application. Sus lecturas y escrituras SQL
están en DataAccess. `OperationsControlService` coordina las lecturas,
`OperationsControlPolicy` aplica las reglas y `OperationsControlRepository` lee
y guarda los datos. Para cerrar caja, el repositorio obtiene los pagos y solicita
la decisión del dominio dentro de la misma transacción serializable, antes de
guardar el cierre. Los mensajes visuales usan `IDialogService` en UI.
En cuotas mensuales, `MonthlyFeeLedgerService` valida el caso de uso en
Application y `MonthlyFeeLedgerRepository` conserva la transacción en DataAccess.
`MonthlyFeePaymentPolicy` decide la elegibilidad de la cuota en Domain.
Las tablas auxiliares deben existir en la base configurada: los modelos EF
describen su estructura, pero la aplicación no ejecuta `CREATE TABLE`
durante una operación. Este proyecto todavía no incluye migraciones EF.

## Regla para cambios nuevos

Un ViewModel no debe construir ni convertir a un repositorio, un contexto EF,
un lector OCR o un servicio SQL concreto. Si necesita una operación, se define
un contrato en la capa que la solicita y se registra su implementación en
`App.xaml.cs`. Los datos visuales y los diálogos permanecen en UI.
