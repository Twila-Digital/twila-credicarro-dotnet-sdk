using System.Net;

namespace Credicarro.Http;

/// <summary>
/// Corpo já materializado em um único <c>byte[]</c>. Os clones da requisição (uma por tentativa de retry e uma para o
/// replay do 401) compartilham o mesmo array, sem copiar o payload, o que importa no envio de documentos (até ~14 MiB).
/// </summary>
internal sealed class BufferedContent : HttpContent
{
    public BufferedContent(byte[] bytes, System.Net.Http.Headers.HttpContentHeaders? headersToCopy = null)
    {
        Bytes = bytes;

        if (headersToCopy is not null)
        {
            foreach (var header in headersToCopy)
                Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    public byte[] Bytes { get; }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        stream.WriteAsync(Bytes, 0, Bytes.Length);

    protected override bool TryComputeLength(out long length)
    {
        length = Bytes.Length;
        return true;
    }
}
