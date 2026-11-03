# ⚡ SoundFlow — Mezclador y Enrutador de Audio Inteligente para Windows

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20(x64)-blue.svg)](#)
[![Tech](https://img.shields.io/badge/.NET%20Framework-4.0%2B-512BD4.svg)](#)
[![Language](https://img.shields.io/badge/Language-C%23-239120.svg)](#)
[![Size](https://img.shields.io/badge/Size-~76%20KB-success.svg)](#)

**SoundFlow** es una aplicación nativa, ultra-liviana y de alto rendimiento para Windows diseñada para darte **control total e individual sobre el sonido de tu PC**. Permite controlar el volumen de cada proceso en tiempo real, visualizar vúmetros en vivo, enrutar aplicaciones a dispositivos de audio específicos (auriculares, parlantes, canales virtuales) y alternar entre perfiles inteligentes con un solo clic.

---

## ✨ Características Principales

### 🎛️ Control de Audio por Aplicación
- **Volumen Individual:** Control independiente (0% a 100%) para cada juego, navegador, reproductor o programa activo.
- **Vúmetro en Tiempo Real (Peak Meter):** Indicador visual responsivo que muestra la actividad de audio que emite cada aplicación segundo a segundo.
- **Silencio Rápido (Mute):** Silencia o activa cualquier programa con un solo clic.
- **Detección Automática de Íconos:** Identifica y muestra el ícono nativo y nombre legible de cada proceso (Discord, Opera GX, Steam, Spotify, etc.).

### 🔀 Enrutamiento Dinámico de Audio
- Asigna salidas diferentes a diferentes aplicaciones sobre la marcha:
  - Envía el audio de tu juego a altavoces dedicados o canales de streaming mientras mantienes Discord y el navegador en tus auriculares.
  - Vuelve a sincronizar cualquier aplicación con el dispositivo predeterminado del sistema con un clic.

### 🎯 Perfiles de Audio Inteligentes
Configuraciones predefinidas y personalizadas para cambiar todo tu entorno sonoro al instante:
- **Modo Streaming:** Redirige juegos y música a salidas de transmisión mientras mantiene comunicaciones en tus auriculares.
- **Modo Gaming (Todo Auriculares):** Concentra todas las fuentes de audio en tus auriculares de juego.
- **Modo Parlantes Setup:** Pasa todo el audio general a los altavoces de escritorio.
- **Modo Noche:** Perfil con niveles balanceados y atenuados para sesiones nocturnas.
- **Perfiles Personalizados:** Guarda la distribución y volúmenes actuales con cualquier nombre para restaurarlos cuando desees.

### ⚡ Acción Rápida "Mover Todo A..."
- Barra de acceso directo para transferir todas las aplicaciones activas a un dispositivo seleccionado en 1 clic, con la opción de establecerlo simultáneamente como dispositivo predeterminado de Windows.

### 🪶 Ultra Ligero y Sin Dependencias Externas
- Desarrollado en **C# nativo** utilizando directamente las APIs de bajo nivel de Windows (**WASAPI** y COM **CoreAudio**).
- No requiere runtimes pesados como Node.js, Electron ni WebView2.
- El ejecutable ocupa únicamente **~76 KB** y consume prácticamente **0% de CPU y memoria mínima** en segundo plano.
- Se integra en la **Bandeja del Sistema (System Tray)** junto al reloj para estar siempre a mano sin estorbar en la barra de tareas.

---

## 🚀 Instalación y Uso

### Ejecutar Directamente
SoundFlow es un ejecutable portátil autónomo:
1. Descarga el ejecutable `SoundFlow.exe`.
2. Ejecútalo. La aplicación se iniciará y se ubicará en la bandeja del sistema (System Tray).

### Consejos de Uso
- **Abrir ventana:** Haz doble clic sobre el ícono de SoundFlow en la bandeja del sistema.
- **Menú Rápido:** Haz clic derecho en el ícono de la bandeja para cambiar rápidamente de perfil o salir del programa.
- **Cierre / Ocultar:** Al presionar el botón `✕` de la ventana, la aplicación se minimiza a la bandeja del sistema manteniéndose activa. Para cerrarla por completo, haz clic derecho en el ícono de la bandeja y selecciona **Salir**.
- **Perfiles Guardados:** Los perfiles personalizados se almacenan automáticamente en `%APPDATA%\SoundFlow\profiles.json`.

---

## 🛠️ Compilación desde el Código Fuente

SoundFlow puede compilarse directamente en cualquier equipo con Windows utilizando el compilador nativo de .NET (`csc.exe`) sin necesidad de instalar Visual Studio:

1. Abre una consola de PowerShell en la carpeta del proyecto.
2. Ejecuta el script de compilación:
   ```powershell
   .\build.ps1
   ```
3. El ejecutable `SoundFlow.exe` se generará automáticamente optimizado para arquitectura x64.

---

## 🏗️ Estructura del Proyecto

- `AudioInterop.cs`: Declaraciones COM y P/Invoke para interactuar con CoreAudio APIs / WASAPI (IMMDeviceEnumerator, IAudioSessionManager2, ISimpleAudioVolume, IAudioMeterInformation, etc.).
- `AudioEngine.cs`: Lógica principal de gestión de dispositivos, enumeración de sesiones de audio, monitoreo de picos y enrutamiento de procesos.
- `Models.cs`: Modelos de datos para sesiones de audio, dispositivos y perfiles.
- `ProfileService.cs`: Serialización y persistencia de perfiles en formato JSON.
- `MainForm.cs`: Ventana principal con interfaz de usuario, sliders de volumen, vúmetros y controles de perfiles.
- `QuickFlyoutForm.cs`: Interfaz compacta de acceso rápido.
- `PrtScnRemapper.cs`: Manejo de atajos globales de teclado.
- `Program.cs`: Punto de entrada de la aplicación, configuración DPI y ciclo de vida de la bandeja del sistema.
- `build.ps1`: Script automatizado para compilar con `csc.exe`.

---

## 📄 Licencia

Este proyecto se distribuye bajo la licencia MIT. Siéntete libre de utilizarlo, modificarlo y adaptarlo a tus necesidades.
