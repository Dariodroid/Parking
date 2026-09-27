using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;

namespace Parking.UI.Windows.Services;

/// <summary>Confirmación breve después de guardar una entrada o salida.</summary>
internal static class ScannerAudioFeedback
{
    private const int SampleRate = 44100;
    private static readonly byte[] EntryTone = CreateTone(1050, 1450);
    private static readonly byte[] ExitTone = CreateTone(1450, 1050);

    /// <summary>Reproduce la señal breve correspondiente a una entrada guardada.</summary>
    public static void PlayEntry() => Play(EntryTone);
    /// <summary>Reproduce la señal breve correspondiente a una salida guardada.</summary>
    public static void PlayExit() => Play(ExitTone);

    /// <summary>Inicia la reproducción sin bloquear la cámara ni la interfaz.</summary>
    /// <param name="wave">Archivo WAV completo generado en memoria.</param>
    private static void Play(byte[] wave)
    {
        // SoundPlayer.PlaySync espera la reproducción completa. Se hace fuera
        // del hilo de la ventana para no detener la cámara ni el cuadro de diálogo.
        _ = Task.Run(() =>
        {
            try
            {
                using var stream = new MemoryStream(wave, writable: false);
                using var player = new SoundPlayer(stream);
                player.PlaySync();
            }
            catch
            {
                // El registro ya fue guardado; un equipo sin audio no debe
                // convertir una operación correcta en un error.
            }
        });
    }

    /// <summary>Compone dos pitidos PCM con una pausa intermedia en formato WAV.</summary>
    /// <param name="firstFrequency">Frecuencia en hercios del primer pitido.</param>
    /// <param name="secondFrequency">Frecuencia en hercios del segundo pitido.</param>
    /// <returns>Bytes del WAV reproducible por SoundPlayer.</returns>
    private static byte[] CreateTone(int firstFrequency, int secondFrequency)
    {
        // Las duraciones se convierten a muestras usando SampleRate.
        const int toneMilliseconds = 85;
        const int pauseMilliseconds = 25;
        int toneSamples = SampleRate * toneMilliseconds / 1000;
        int pauseSamples = SampleRate * pauseMilliseconds / 1000;
        int totalSamples = toneSamples * 2 + pauseSamples;
        int dataSize = totalSamples * sizeof(short);

        // La cabecera RIFF/WAVE describe PCM mono de 16 bits.
        using var stream = new MemoryStream(44 + dataSize);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write((short)1); // Mono
        writer.Write(SampleRate);
        writer.Write(SampleRate * sizeof(short));
        writer.Write((short)sizeof(short));
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(dataSize);

        // Se escriben primer tono, silencio y segundo tono en ese orden.
        WriteBeep(writer, firstFrequency, toneSamples);
        for (int i = 0; i < pauseSamples; i++) writer.Write((short)0);
        WriteBeep(writer, secondFrequency, toneSamples);
        return stream.ToArray();
    }

    /// <summary>Escribe las muestras de un pitido con entrada y salida suaves.</summary>
    /// <param name="writer">Destino binario del audio PCM.</param>
    /// <param name="frequency">Frecuencia del tono en hercios.</param>
    /// <param name="samples">Cantidad de muestras que debe escribir.</param>
    private static void WriteBeep(BinaryWriter writer, int frequency, int samples)
    {
        int fadeSamples = SampleRate / 200; // 5 ms de entrada y salida suaves
        for (int i = 0; i < samples; i++)
        {
            // La envolvente evita chasquidos al comenzar y terminar el tono.
            double fade = Math.Min(1.0, Math.Min(i, samples - 1 - i) / (double)fadeSamples);
            double value = Math.Sin(2 * Math.PI * frequency * i / SampleRate) * fade;
            writer.Write((short)(value * 9000));
        }
    }
}
