using NVorbis;

namespace Pokedex1
{
    public static class OggConverter
    {
        public static async Task<Stream> FetchOggAsWavAsync(string url)
        {
            Console.WriteLine($"[OggConverter] Fetching OGG from: {url}");
            using var http = new HttpClient();
            var oggBytes = await http.GetByteArrayAsync(url);
            Console.WriteLine($"[OggConverter] Downloaded {oggBytes.Length} bytes");

            using var oggStream = new MemoryStream(oggBytes);
            using var reader = new VorbisReader(oggStream, true);

            Console.WriteLine($"[OggConverter] Decoded - Channels: {reader.Channels}, SampleRate: {reader.SampleRate}");

            var outputStream = new MemoryStream();

            // Write WAV header placeholder (we'll fill it in after)
            WriteWavHeader(outputStream, reader.Channels, reader.SampleRate, 0);

            // Decode OGG to PCM samples
            var buffer = new float[reader.SampleRate * reader.Channels];
            int samplesRead;
            var allSamples = new List<short>();

            while ((samplesRead = reader.ReadSamples(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < samplesRead; i++)
                {
                    // Convert float PCM to 16-bit PCM
                    short sample = (short)Math.Clamp(buffer[i] * 32767f, short.MinValue, short.MaxValue);
                    allSamples.Add(sample);
                }
            }

            Console.WriteLine($"[OggConverter] Total samples decoded: {allSamples.Count}");

            // Write PCM data
            foreach (var sample in allSamples)
            {
                outputStream.Write(BitConverter.GetBytes(sample), 0, 2);
            }

            // Go back and fix the WAV header with correct sizes
            int dataSize = allSamples.Count * 2;
            outputStream.Seek(0, SeekOrigin.Begin);
            WriteWavHeader(outputStream, reader.Channels, reader.SampleRate, dataSize);

            outputStream.Seek(0, SeekOrigin.Begin);
            Console.WriteLine($"[OggConverter] WAV stream ready, total size: {outputStream.Length} bytes");

            return outputStream;
        }

        private static void WriteWavHeader(Stream stream, int channels, int sampleRate, int dataSize)
        {
            var bw = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            bw.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"));
            bw.Write(36 + dataSize);
            bw.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"));
            bw.Write(System.Text.Encoding.UTF8.GetBytes("fmt "));
            bw.Write(16);               // PCM chunk size
            bw.Write((short)1);         // PCM format
            bw.Write((short)channels);
            bw.Write(sampleRate);
            bw.Write(sampleRate * channels * 2); // byte rate
            bw.Write((short)(channels * 2));     // block align
            bw.Write((short)16);        // bits per sample
            bw.Write(System.Text.Encoding.UTF8.GetBytes("data"));
            bw.Write(dataSize);
        }
    }
}
