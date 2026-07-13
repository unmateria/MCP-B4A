[English](README.md) · **Español**

![mcp-b4a](https://github.com/user-attachments/assets/115aa63b-3a12-4408-8ae1-479290cfca2f)


# Servidor MCP para B4A

Conecta [Claude Code](https://claude.ai/claude-code) (y cualquier cliente compatible con MCP) con el ecosistema [B4A](https://www.b4x.com/b4a.html) (Basic4Android).

Expone 31 herramientas para compilar proyectos, leer/editar módulos de código fuente, leer/modificar layouts, explorar librerías, desplegar APKs, depurar por ADB, verificar la interfaz visualmente en el propio dispositivo y limpiar artefactos de PNG de sprites — todo sin salir de tu asistente de programación con IA.

---

## Herramientas

### Configuración

| Herramienta | Descripción |
|------|-------------|
| `b4a_get_config` | Devuelve las rutas y orígenes de configuración actuales (autodetectados vs. explícitos) |
| `b4a_set_config` | Actualiza un valor de configuración |

### Compilación y despliegue

| Herramienta | Descripción |
|------|-------------|
| `b4a_build` | Compila un proyecto B4A mediante B4ABuilder.exe (release/debug/bundle) |
| `b4a_build_and_install` | Compila **e** instala en un solo paso — equivale a `b4a_build` + `b4a_install_apk` |
| `b4a_get_build_log` | Devuelve el registro de la última compilación |
| `b4a_get_signing_info` | Devuelve la ruta del keystore, el alias y el estado de firma (las contraseñas nunca se exponen) |
| `b4a_install_apk` | Instala un APK en un dispositivo conectado por ADB |

### Proyecto

| Herramienta | Descripción |
|------|-------------|
| `b4a_read_project` | Lee los metadatos del proyecto: librerías, módulos, configuraciones de compilación |
| `b4a_list_project_files` | Lista archivos fuente, layouts y recursos |
| `b4a_project_context` | Resumen en una sola llamada: info de la app, librerías, módulos y último error de compilación |
| `b4a_list_recent_projects` | Lista los proyectos abiertos recientemente en el historial del IDE de B4A |

### Layouts

| Herramienta | Descripción |
|------|-------------|
| `b4a_read_layout` | Convierte el binario .bal/.bil a JSON |
| `b4a_write_layout` | Escribe el JSON de vuelta a .bal/.bil (con validación y copia de seguridad) |
| `b4a_list_layouts` | Lista todos los archivos de layout de un directorio de proyecto |

### Librerías

| Herramienta | Descripción |
|------|-------------|
| `b4a_list_libraries` | Lista las librerías B4A disponibles con información de versión |
| `b4a_get_library_docs` | Devuelve la documentación formateada de métodos/propiedades/eventos de una librería |
| `b4a_search_library` | Busca en toda la documentación de las librerías |

### Módulos de código fuente

| Herramienta | Descripción |
|------|-------------|
| `b4a_read_bas` | Lee un módulo `.bas` y devuelve su contenido con números de línea |
| `b4a_edit_bas` | Edición por buscar-y-reemplazar sobre un archivo `.bas`. Coincide con el texto exacto (incluida la indentación), normaliza los finales de línea y crea copia `.bak`. Rechaza coincidencias ambiguas salvo que `replace_all=true`. |

### Manifiesto

| Herramienta | Descripción |
|------|-------------|
| `b4a_read_manifest` | Extrae el bloque del Editor de Manifiesto de un archivo .b4a |
| `b4a_write_manifest` | Actualiza el bloque del Editor de Manifiesto |

### Interacción con el dispositivo

Permite verificar la interfaz visualmente y controlar el dispositivo sin salir de Claude Code. Sustituye el flujo manual de capturas con Bash + PIL.

| Herramienta | Descripción |
|------|-------------|
| `b4a_screenshot` | Captura un PNG del dispositivo. Opcionalmente `cropX/Y/W/H` para subregiones y `delayMs` para esperar antes de capturar |
| `b4a_pixel_scan` | Lee valores RGB de un PNG guardado — puntos individuales (pares `x,y`) o una malla de una región completa |
| `b4a_tap` | Envía un evento de toque en las coordenadas `(x, y)` del dispositivo |
| `b4a_swipe` | Envía un gesto de deslizamiento de `(x1,y1)` a `(x2,y2)` con duración configurable |
| `b4a_launch_app` | Lanza una app instalada mediante `adb shell am start` |
| `b4a_key_event` | Envía un evento de tecla de Android (BACK=4, HOME=3, MENU=82, ENTER=66, …) |
| `b4a_input_text` | Escribe texto en el campo de entrada enfocado (los espacios como `%s`) |

### Herramientas de sprites

| Herramienta | Descripción |
|------|-------------|
| `b4a_sprite_cleanup` | Limpia archivos PNG de sprites generados por Gemini: elimina artefactos de borde blancos/grises (líneas divisorias residuales, antialiasing) y, opcionalmente, recorta el relleno transparente. Crea copia `.bak` antes de sobrescribir. Admite un solo archivo o lotes con `*.png`. **Usa `autoCrop=false` para sprites de juego** — el autorecorte cambia las dimensiones del lienzo y deforma los sprites cuando el juego los escala a un tamaño de visualización fijo. |

### ADB / Depuración

| Herramienta | Descripción |
|------|-------------|
| `b4a_get_logcat` | Devuelve la salida de logcat filtrada por la etiqueta de B4A. Muestra el prefijo `[showing last N of M lines]` cuando la salida está truncada. |
| `b4a_list_devices` | Lista los dispositivos ADB conectados |

---

## Requisitos previos

- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (o usa la compilación self-contained)
- [IDE de B4A](https://www.b4x.com/b4a.html) instalado (necesario para compilar)
- [Android SDK Platform Tools](https://developer.android.com/tools/releases/platform-tools) (necesario para las herramientas de ADB)

---

## Instalación

### 1. Consigue el binario

**Recomendado — descarga el ejecutable ya compilado:** obtén `B4aMcp.exe` desde la
[página de Releases](https://github.com/unmateria/MCP-B4A/releases/latest). Es una
compilación self-contained para Windows — no requiere el runtime de .NET.

**Alternativa — compilar desde el código fuente:**

```powershell
cd B4aMcp
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ../publish
```

### 2. Configura Claude Code

```bash
claude mcp add b4a C:\ruta\a\B4aMcp.exe
```

O añádelo manualmente a tu configuración de MCP:

```json
{
  "mcpServers": {
    "b4a": {
      "command": "C:\\ruta\\a\\B4aMcp.exe",
      "args": []
    }
  }
}
```

### 3. Configuración inicial

Ejecuta `b4a_get_config` para comprobar las rutas autodetectadas. Si B4A está instalado en una ubicación no estándar, configúralo manualmente:

```
b4a_set_config(key="b4aPath", value="C:\\B4A")
b4a_set_config(key="additionalLibrariesPath", value="C:\\B4A\\SharedLibs")
```

El servidor autodetecta las rutas desde el archivo de ajustes del IDE de B4A (`b4xV5.ini`) cuando es posible. La configuración se guarda en `%APPDATA%\mcp-b4a\config.json`.

---

## Archivos de layout (.bal / .bil)

B4A guarda los layouts de la interfaz en un formato binario propietario. Este servidor incluye un port completo del [BalConverter](https://github.com/B4X-Community/B4X-BalConverter) oficial a VB.NET, que ofrece conversión binario-a-JSON de ida y vuelta sin pérdidas.

- `.bal` — layout estándar (layouts de Activity)
- `.bil` — variante interna del layout (las entradas RECT32/CNULL se eliminan al escribir)

**Seguridad en la ida y vuelta:** `b4a_write_layout` valida la estructura del JSON y siempre crea una copia `.bak` antes de escribir.

### Propiedades obligatorias de EditText

Los controles EditText del JSON de layout requieren propiedades específicas que difieren de otros tipos de control. El runtime de B4A **no** protege contra nulos todas ellas, así que las propiedades ausentes provocan cierres en tiempo de ejecución.

| Propiedad | Tipo | Obligatoria | Por defecto | Notas |
|----------|------|----------|---------|-------|
| `inputType` | string | Sí | — | `TEXT`, `NUMBERS`, `DECIMAL_NUMBERS`, `PHONE`, `NONE` |
| `password` | boolean | **Sí** | — | Sin protección contra nulos en el runtime de B4A — **se cierra si falta** |
| `hint` | string | No | `""` | Texto de marcador de posición |
| `hintColor` | color | No | `0xFFF0F0F0` | Usa el formato `ValueType: 6` |
| `singleLine` | boolean | No | `true` | Comportamiento de ajuste de texto |
| `wrap` | boolean | No | `true` | Tiene valor por defecto en el runtime |
| `forceDone` | boolean | No | `false` | Tiene valor por defecto en el runtime |

> **Los valores de `inputType` deben ser constantes de tipo string, NO enteros.** B4A usa reflexión de Java (`getField("INPUT_TYPE_" + value)`) internamente, así que valores numéricos como `1` o `129` provocarán un cierre en tiempo de ejecución con `NoSuchFieldException`.

`b4a_write_layout` inyecta automáticamente `password` y `singleLine` ausentes con valores por defecto seguros, y convierte los valores numéricos de `inputType` a sus equivalentes con nombre cuando es posible. Las advertencias se incluyen en la respuesta.

---

## Escollo habitual de B4A: el tamaño de fuente de DrawText es en dp, no en píxeles

`Canvas.DrawText` en B4A interpreta el parámetro de tamaño de fuente como **dp** (píxeles independientes de la densidad). Android lo multiplica por `deviceDpi / 160` internamente, así que el mismo tamaño numérico de fuente se renderiza a tamaños de píxel muy distintos entre dispositivos. Si los contenedores de la interfaz (bitmaps, rects) usan dimensiones fijas en píxeles pero las fuentes escalan con el DPI, **el texto se desbordará en dispositivos de DPI bajo**.

**Regla práctica:** toda dimensión de la interfaz — tamaños de contenedor, tamaños de fuente, posiciones y zonas de toque — debería ser **proporcional a `100%x` / `100%y`** (anchura/altura de pantalla en píxeles). Nunca uses valores fijos en píxeles como `Dim btnW As Int = 200`.

Consulta `b4a_language_gotchas` para más escollos específicos de B4A.

---

## Detalles de implementación destacables

- **WorkingDirectory de `b4a_build`**: `B4ABuilder.exe` se invoca con `WorkingDirectory = baseFolder`. Sin esto, el compilador no puede localizar el archivo `.b4a` aunque se pase `-BaseFolder`, porque resuelve el nombre del proyecto contra el directorio de trabajo actual del proceso que lo llama.

- **Seguridad al escribir el manifiesto**: `b4a_write_manifest` usa una lambda `MatchEvaluator` en lugar de un reemplazo de cadena para evitar que `Regex.Replace` interprete las retrorreferencias tipo `$0`/`\1` del contenido del manifiesto. Siempre se escribe una copia `.bak` antes de modificar el archivo.

- **Resiliencia de la configuración**: si `%APPDATA%\mcp-b4a\config.json` está malformado, el servidor recurre a una configuración vacía y autodetecta las rutas desde `b4xV5.ini` en lugar de fallar al arrancar.

---

## Seguridad

- `b4a_build` solo acepta rutas a archivos `.b4a` existentes — sin ejecución arbitraria de comandos.
- Toda escritura de archivos crea primero copias `.bak`.
- Las herramientas de ADB que modifican el estado del dispositivo (`b4a_tap`, `b4a_swipe`, `b4a_launch_app`, `b4a_key_event`, `b4a_input_text`, `b4a_install_apk`) solo interactúan con el dispositivo Android conectado, no con el sistema de archivos del host.
- Las contraseñas de firma de `b4xV5.ini` nunca se exponen a través de `b4a_get_signing_info`.

---

## Caché

El servidor cachea resultados para evitar E/S redundante:

- **Caché basada en archivos** (invalidada por mtime): conversiones de layout, parseo de proyectos, documentación de librerías
- **Caché TTL** (60 s): lista de dispositivos, listados de librerías
- **Almacén simple**: registro de la última compilación (sin caducidad, reemplazado en cada build)

La caché se invalida automáticamente cuando cambian los archivos fuente.

---

## Desarrollo

```powershell
cd B4aMcp
dotnet build
dotnet run
```

El servidor se comunica por **stdio** (estándar MCP). No abre ningún puerto de red.

### Estructura del proyecto

```
B4aMcp/
├── Program.vb              # Punto de entrada del servidor
├── Models/
│   ├── B4aProject.vb       # Modelo de metadatos del proyecto
│   └── McpConfig.vb        # Modelo de configuración
├── Tools/
│   ├── AdbTools.vb          # logcat, dispositivos, instalar APK
│   ├── BuildTools.vb        # compilar, combo build+install, registro de build, info de firma
│   ├── ConfigTools.vb       # obtener/establecer configuración
│   ├── DeviceTools.vb       # captura, tap, swipe, lanzar app, escaneo de píxeles, evento de tecla, entrada de texto
│   ├── LayoutTools.vb       # leer/escribir/listar layouts + validación de EditText
│   ├── LibraryTools.vb      # listar, docs, buscar librerías
│   ├── BasTools.vb           # leer/editar módulos de código .bas
│   ├── ManifestTools.vb     # leer/escribir bloque de manifiesto
│   ├── ProjectTools.vb      # metadatos del proyecto, listado de archivos, contexto
│   └── SpriteTools.vb       # limpieza de PNG de sprites: eliminación de artefactos de borde + autorecorte
└── Utils/
    ├── AppConfig.vb          # Gestión de configuración + autodetección del IDE de B4A
    ├── B4aParser.vb          # Parser de archivos de proyecto .b4a
    ├── BalConverter.vb        # Convertidor binario .bal/.bil ↔ JSON
    └── CacheManager.vb       # Caché basada en mtime + TTL
```

### Tecnologías

- **VB.NET** sobre **.NET 8**
- SDK de [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) (0.9.0-preview.2)
- [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json) para serialización
- [System.Drawing.Common](https://www.nuget.org/packages/System.Drawing.Common) para lectura de píxeles de PNG (solo Windows)

---

## Licencia

MIT
