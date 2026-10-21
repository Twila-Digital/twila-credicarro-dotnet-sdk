using System.Text.Json;
using Credicarro.Proposals.Models;
using Credicarro.Serialization;
using Credicarro.Simulations.Models;
using Credicarro.Webhooks.Models;

namespace Credicarro.UnitTests.Serialization;

public class UnknownEnumJsonConverterFactoryTests
{
    private sealed record Holder(ProposalStatus Status);

    [Theory]
    [InlineData(1, ProposalStatus.Analysing)]
    [InlineData(2, ProposalStatus.Approved)]
    [InlineData(9, ProposalStatus.Pending)]
    public void Read_ComValorConhecido_MapeiaParaOMembro(int value, ProposalStatus expected)
    {
        var holder = JsonSerializer.Deserialize<Holder>($"{{\"status\":{value}}}", CredicarroJsonOptions.Default);

        Assert.Equal(expected, holder!.Status);
    }

    [Theory]
    [InlineData("99")]
    [InlineData("-5")]
    [InlineData("\"texto\"")]
    public void Read_ComValorDesconhecidoOuInvalido_MapeiaParaUnknown_SemLancar(string raw)
    {
        var holder = JsonSerializer.Deserialize<Holder>($"{{\"status\":{raw}}}", CredicarroJsonOptions.Default);

        Assert.Equal(ProposalStatus.Unknown, holder!.Status);
    }

    [Fact]
    public void Write_EscreveOValorNumerico()
    {
        var json = JsonSerializer.Serialize(new Holder(ProposalStatus.Completed), CredicarroJsonOptions.Default);

        Assert.Equal("{\"Status\":6}", json);
    }

    [Fact]
    public void EnumMapping_FromWireValue_ValorNaoDefinidoViraUnknown()
    {
        Assert.Equal(DocumentType.Unknown, EnumMapping.FromWireValue<DocumentType>(10));
        Assert.Equal(DocumentType.Atpv, EnumMapping.FromWireValue<DocumentType>(57));
        Assert.Equal(ProposalStep.Undefined, EnumMapping.FromWireValue<ProposalStep>(0));
        Assert.Equal(ActionOwner.Unknown, EnumMapping.FromWireValue<ActionOwner>(7));
    }

    [Theory]
    [InlineData("Proposal", WebhookType.Proposal)]
    [InlineData("proposal", WebhookType.Proposal)]
    [InlineData("3", WebhookType.Proposal)]
    [InlineData("Customer", WebhookType.Customer)]
    [InlineData("Alguma", WebhookType.Unknown)]
    [InlineData("", WebhookType.Unknown)]
    [InlineData(null, WebhookType.Unknown)]
    public void EnumMapping_FromWireName_AceitaNomeNumeroEFallback(string? name, WebhookType expected)
    {
        Assert.Equal(expected, EnumMapping.FromWireName<WebhookType>(name));
    }

    [Fact]
    public void EnumMapping_FromWireName_LeOTipoDaSimulacaoPeloNome()
    {
        Assert.Equal(SimulationType.Financing, EnumMapping.FromWireName<SimulationType>("Financing"));
        Assert.Equal(SimulationType.AmountSimulation, EnumMapping.FromWireName<SimulationType>("AmountSimulation"));
        Assert.Equal(SimulationType.Unknown, EnumMapping.FromWireName<SimulationType>("Leasing"));
    }

    [Fact]
    public void TodosOsEnumsDeLeitura_TemUmMembroUnknownIgualAMenosUm()
    {
        var enums = new[]
        {
            typeof(ProposalStatus), typeof(ProposalType), typeof(ProposalStep), typeof(StepSituation), typeof(ActionOwner),
            typeof(BiometryStatus), typeof(DocumentStatus), typeof(DocumentType), typeof(SimulationType), typeof(WebhookType),
            typeof(WebhookAuthenticationType), typeof(WebhookStatus), typeof(ProposalEventReason)
        };

        foreach (var type in enums)
        {
            var unknown = UnknownEnumJsonConverterFactory.FindUnknownMember(type);

            Assert.NotNull(unknown);
            Assert.Equal(-1, Convert.ToInt32(unknown!.GetValue(null)));
        }
    }
}
