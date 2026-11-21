# Sistema de Análisis de Opiniones de Clientes

**Electiva 1 · Actividad 1 · Extracción ETL en .NET 8**  
**Hansel Emir Hernández Félix**

El Worker extrae respuestas de encuestas desde CSV, reseñas desde una base relacional y comentarios desde una API REST. Los resultados se guardan en archivos NDJSON en el área de staging. El informe documenta la arquitectura completa hasta el tablero analítico e identifica las etapas previstas de transformación y carga.

## Ejecutar la demostración

Requisitos: SDK de .NET 8, Python 3 y acceso a NuGet para la primera compilación. Trabaja desde la raíz del proyecto. En Windows puedes usar `py` en lugar de `python`.

1. En una terminal, ejecuta `python demo/servidor_demo.py`. El script prepara SQLite y sirve la API en el puerto local 8765. Deja abierta esa terminal.
2. En otra terminal, ejecuta `dotnet run --project src`.
3. Abre `staging/<runId>/`. Debe contener `encuestas-csv.ndjson` (2 filas), `resenas-bd.ndjson` (3 filas) y `comentarios-api.ndjson` (3 filas).
4. Detén la API con Ctrl+C al terminar.

La demostración reutiliza datos ficticios del proyecto anterior para CSV y reseñas, y añade tres comentarios de ejemplo. Los clientes, opiniones y vínculos del modelo anterior se identifican mediante los ID originales incluidos en los archivos.

## Conectar al SQL Server anterior

Se requiere que `analisis_opiniones` tenga creadas y pobladas las tablas `dbo.opinion` y `dbo.resena_web`. La cuenta del Worker necesita permiso SELECT sobre esas dos tablas. Usa autenticación integrada en Windows cuando ya tengas una cuenta con acceso y un nombre de servidor que coincida con el certificado.

Ejemplo en PowerShell, reemplazando el nombre de servidor:

```powershell
$env:Extraction__Database__Provider = 'SqlServer'
$env:ConnectionStrings__Reviews = 'Server=SERVIDOR;Database=analisis_opiniones;Integrated Security=True;Encrypt=True;TrustServerCertificate=False'
dotnet run --project src
```

El ejemplo mantiene el CSV y la API de demostración habilitados. Para cambiar sus orígenes, configura `Extraction__Csv__Path` y `Extraction__Api__Url`. La API debe devolver un arreglo JSON con objetos que tienen un campo `id` de tipo texto o número. Para una API autenticada, guarda el token como `Extraction__Api__Token` mediante el gestor de secretos del entorno. Un origen remoto requiere HTTPS. Cada fuente puede deshabilitarse por separado con su ajuste `Enabled`.

El archivo `src/appsettings.json` contiene los valores de demostración. Las variables de entorno sustituyen esos valores. También se admite User Secrets cuando `DOTNET_ENVIRONMENT` es `Development`. La carpeta staging debe ser privada para el usuario del proceso.

## Estados y salidas

Cada lote confirmado tiene su propia carpeta. Las filas conservan `runId`, `source`, `sourceId` y `payload`. Usa `(source, sourceId)` como identidad de origen en una transformación posterior. Cada ejecución vuelve a leer las fuentes habilitadas. Dos ejecuciones válidas sobre los mismos datos producen lotes distintos que pueden repetir los mismos ID de origen.

El Worker devuelve 0 al confirmar el lote, 1 ante un fallo de extracción y 2 si se cancela durante el procesamiento. Un fallo HTTP, SQL o de formato impide confirmar el lote completo. Una interrupción abrupta puede dejar una carpeta `.pending-*`, que el consumidor debe ignorar.

## Estructura

| Ruta | Contenido |
| --- | --- |
| `src/Application` | Worker, opciones y contratos para extracción y staging |
| `src/Infrastructure` | Adaptadores de CSV, SQL y HTTP, y escritura NDJSON |
| `demo` | Datos ficticios de prueba y API local |
| `docs` | Diagramas editables y evidencias de ejecución |

La ruta SQL Server está implementada contra el modelo de la entrega anterior. La verificación reproducible incluida usa SQLite para la fuente relacional y la API local.
