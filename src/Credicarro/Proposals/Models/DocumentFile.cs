namespace Credicarro.Proposals.Models;

/// <summary>
/// Arquivo de um documento da proposta. As regras da API são validadas na criação: nome sem caminho com até 255
/// caracteres, extensão <c>.pdf</c>, <c>.jpg</c>, <c>.jpeg</c> ou <c>.png</c> e conteúdo de 1 byte a 10 MiB.
/// A extensão precisa corresponder ao conteúdo real do arquivo (a API valida e responde <c>Document.InvalidContent</c>).
/// <para>
/// O conteúdo é materializado uma única vez: o mesmo <c>byte[]</c> é reaproveitado em todas as tentativas e no replay do
/// 401. <see cref="FromBytes"/> guarda a referência recebida, então não altere o array depois de criar o arquivo.
/// </para>
/// </summary>
public sealed class DocumentFile
{
    /// <summary>Tamanho máximo do arquivo (10 MiB, depois de decodificado).</summary>
    public static int MaxSizeInBytes { get; } = 10 * 1024 * 1024;

    private const int MaxFileNameLength = 255;
    private const int ChunkSize = 81920;

    private static readonly string[] AllowedExtensions = [".pdf", ".jpg", ".jpeg", ".png"];

    private DocumentFile(string fileName, byte[] content)
    {
        FileName = fileName;
        Content = content;
    }

    public string FileName { get; }

    /// <summary>Conteúdo bruto, enviado como Base64 puro (sem prefixo <c>data:</c> nem quebras de linha).</summary>
    internal byte[] Content { get; }

    /// <summary>Base64 do conteúdo. Só para testes: o envio serializa os bytes direto, sem string intermediária.</summary>
    internal string Base64Content => Convert.ToBase64String(Content);

    /// <summary>Não inclui o nome nem o conteúdo (podem conter dados pessoais).</summary>
    public override string ToString() => $"DocumentFile {{ Size = {Content.Length} }}";

    public static DocumentFile FromBytes(byte[] content, string fileName)
    {
        if (content is null)
            throw new ArgumentNullException(nameof(content));

        ValidateFileName(fileName);

        if (content.Length == 0)
            throw new ArgumentException("O arquivo está vazio.", nameof(content));

        if (content.Length > MaxSizeInBytes)
            throw new ArgumentException("O arquivo excede o limite de 10 MiB.", nameof(content));

        return new DocumentFile(fileName, content);
    }

    public static DocumentFile FromStream(Stream content, string fileName)
    {
        if (content is null)
            throw new ArgumentNullException(nameof(content));

        ValidateFileName(fileName);

        using var buffer = new MemoryStream();
        var chunk = new byte[ChunkSize];

        while (true)
        {
            var toRead = (int)Math.Min(chunk.Length, (long)MaxSizeInBytes + 1 - buffer.Length);

            if (toRead <= 0)
                throw new ArgumentException("O arquivo excede o limite de 10 MiB.", nameof(content));

            var read = content.Read(chunk, 0, toRead);

            if (read <= 0)
                break;

            buffer.Write(chunk, 0, read);
        }

        return FromBytes(buffer.ToArray(), fileName);
    }

    public static async Task<DocumentFile> FromStreamAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        if (content is null)
            throw new ArgumentNullException(nameof(content));

        ValidateFileName(fileName);

        using var buffer = new MemoryStream();
        var chunk = new byte[ChunkSize];

        while (true)
        {
            var toRead = (int)Math.Min(chunk.Length, (long)MaxSizeInBytes + 1 - buffer.Length);

            if (toRead <= 0)
                throw new ArgumentException("O arquivo excede o limite de 10 MiB.", nameof(content));

            var read = await content.ReadAsync(chunk, 0, toRead, cancellationToken).ConfigureAwait(false);

            if (read <= 0)
                break;

            buffer.Write(chunk, 0, read);
        }

        return FromBytes(buffer.ToArray(), fileName);
    }

    /// <summary>
    /// Lê o arquivo do disco em laço, com limite de 10 MiB + 1 (nunca confia em <c>Length</c>, que pode ser 0 em
    /// arquivos especiais). O nome enviado à API é só o nome do arquivo, sem o caminho.
    /// </summary>
    public static DocumentFile FromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("O caminho do arquivo é obrigatório.", nameof(path));

        var fileName = Path.GetFileName(path);
        ValidateFileName(fileName);

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return FromStream(stream, fileName);
    }

    private static void ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("O nome do arquivo é obrigatório.", nameof(fileName));

        if (fileName.Length > MaxFileNameLength)
            throw new ArgumentException("O nome do arquivo deve ter até 255 caracteres.", nameof(fileName));

        if (fileName.IndexOf('/') >= 0 || fileName.IndexOf('\\') >= 0)
            throw new ArgumentException("O nome do arquivo não pode conter caminho.", nameof(fileName));

        var extension = Path.GetExtension(fileName);

        if (Array.FindIndex(AllowedExtensions, allowed => string.Equals(allowed, extension, StringComparison.OrdinalIgnoreCase)) < 0)
            throw new ArgumentException("A extensão do arquivo deve ser .pdf, .jpg, .jpeg ou .png.", nameof(fileName));
    }
}
