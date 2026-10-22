using System.Text;
using Credicarro.Proposals.Models;

namespace Credicarro.UnitTests.Proposals;

public class DocumentFileTests
{
    private static readonly byte[] SampleBytes = Encoding.UTF8.GetBytes("conteúdo do documento");

    [Fact]
    public void FromBytes_CodificaOConteudoEmBase64Puro()
    {
        var file = DocumentFile.FromBytes(SampleBytes, "cnh.pdf");

        Assert.Equal(Convert.ToBase64String(SampleBytes), file.Base64Content);
        Assert.Equal("cnh.pdf", file.FileName);
        Assert.DoesNotContain("data:", file.Base64Content, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", file.Base64Content, StringComparison.Ordinal);
    }

    [Fact]
    public void FromBytes_ConteudoGrande_NaoQuebraLinhasNoBase64()
    {
        var file = DocumentFile.FromBytes(new byte[500_000], "grande.png");

        Assert.DoesNotContain("\n", file.Base64Content, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", file.Base64Content, StringComparison.Ordinal);
    }

    [Fact]
    public void FromStream_ProduzOMesmoBase64QueFromBytes()
    {
        using var stream = new MemoryStream(SampleBytes);

        var file = DocumentFile.FromStream(stream, "cnh.pdf");

        Assert.Equal(Convert.ToBase64String(SampleBytes), file.Base64Content);
    }

    [Fact]
    public async Task FromStreamAsync_ProduzOMesmoBase64QueFromBytes()
    {
        using var stream = new MemoryStream(SampleBytes);

        var file = await DocumentFile.FromStreamAsync(stream, "cnh.pdf");

        Assert.Equal(Convert.ToBase64String(SampleBytes), file.Base64Content);
    }

    [Fact]
    public void FromFile_UsaSoONomeDoArquivoSemOCaminho()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            File.WriteAllBytes(path, SampleBytes);

            var file = DocumentFile.FromFile(path);

            Assert.Equal(Convert.ToBase64String(SampleBytes), file.Base64Content);
            Assert.Equal(Path.GetFileName(path), file.FileName);
            Assert.DoesNotContain(Path.DirectorySeparatorChar, file.FileName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("doc.pdf")]
    [InlineData("foto.jpg")]
    [InlineData("foto.jpeg")]
    [InlineData("scan.png")]
    [InlineData("MAIUSCULA.PDF")]
    [InlineData("com.varios.pontos.png")]
    public void FromBytes_ComExtensoesAceitas_Funciona(string fileName)
    {
        var file = DocumentFile.FromBytes(SampleBytes, fileName);

        Assert.Equal(fileName, file.FileName);
    }

    [Theory]
    [InlineData("doc.txt")]
    [InlineData("doc.gif")]
    [InlineData("doc.docx")]
    [InlineData("semextensao")]
    [InlineData("termina-com-ponto.")]
    public void FromBytes_ComExtensaoNaoAceita_Lanca(string fileName)
    {
        Assert.Throws<ArgumentException>(() => DocumentFile.FromBytes(SampleBytes, fileName));
    }

    [Theory]
    [InlineData("pasta/doc.pdf")]
    [InlineData("pasta\\doc.pdf")]
    [InlineData("../doc.pdf")]
    [InlineData("C:\\temp\\doc.pdf")]
    public void FromBytes_ComCaminhoNoNome_Lanca(string fileName)
    {
        Assert.Throws<ArgumentException>(() => DocumentFile.FromBytes(SampleBytes, fileName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromBytes_SemNome_Lanca(string? fileName)
    {
        Assert.Throws<ArgumentException>(() => DocumentFile.FromBytes(SampleBytes, fileName!));
    }

    [Fact]
    public void FromBytes_ComNomeDe255Caracteres_FuncionaE256Lanca()
    {
        var ok = new string('a', 251) + ".pdf";
        var tooLong = new string('a', 252) + ".pdf";

        Assert.Equal(255, ok.Length);
        Assert.Equal(ok, DocumentFile.FromBytes(SampleBytes, ok).FileName);
        Assert.Throws<ArgumentException>(() => DocumentFile.FromBytes(SampleBytes, tooLong));
    }

    [Fact]
    public void FromBytes_ComConteudoVazio_Lanca()
    {
        Assert.Throws<ArgumentException>(() => DocumentFile.FromBytes([], "doc.pdf"));
    }

    [Fact]
    public void FromBytes_ComUmByte_Funciona()
    {
        Assert.Equal(Convert.ToBase64String(new byte[] { 1 }), DocumentFile.FromBytes(new byte[] { 1 }, "doc.pdf").Base64Content);
    }

    [Fact]
    public void FromBytes_ComExatamente10MiB_FuncionaE10MiBMaisUmByteLanca()
    {
        var limit = new byte[10 * 1024 * 1024];

        Assert.NotNull(DocumentFile.FromBytes(limit, "doc.pdf"));
        Assert.Throws<ArgumentException>(() => DocumentFile.FromBytes(new byte[limit.Length + 1], "doc.pdf"));
    }

    [Fact]
    public void FromStream_ComMaisDe10MiB_LancaSemPrecisarLerTudo()
    {
        using var stream = new MemoryStream(new byte[10 * 1024 * 1024 + 1]);

        Assert.Throws<ArgumentException>(() => DocumentFile.FromStream(stream, "doc.pdf"));
    }

    [Fact]
    public async Task FromStreamAsync_ComMaisDe10MiB_Lanca()
    {
        using var stream = new MemoryStream(new byte[10 * 1024 * 1024 + 1]);

        await Assert.ThrowsAsync<ArgumentException>(() => DocumentFile.FromStreamAsync(stream, "doc.pdf"));
    }

    [Fact]
    public void FromBytes_ComConteudoNulo_LancaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => DocumentFile.FromBytes(null!, "doc.pdf"));
    }

    [Fact]
    public void FromFile_ComArquivoInexistente_LancaIOException()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pdf");

        Assert.ThrowsAny<IOException>(() => DocumentFile.FromFile(path));
    }

    [Fact]
    public void FromFile_SemCaminho_Lanca()
    {
        Assert.Throws<ArgumentException>(() => DocumentFile.FromFile(""));
    }
}
