using HealthcareEdi.Core.Attributes;

namespace HealthcareEdi.Core.Envelopes;

/// <summary>
/// ISA - Interchange Control Header. Fixed 16 elements.
/// </summary>
[EdiSegment("ISA")]
public sealed class IsaSegment
{
    public string AuthorizationQualifier { get; set; } = string.Empty;       // ISA01
    public string AuthorizationInformation { get; set; } = string.Empty;     // ISA02
    public string SecurityQualifier { get; set; } = string.Empty;            // ISA03
    public string SecurityInformation { get; set; } = string.Empty;          // ISA04
    public string SenderIdQualifier { get; set; } = string.Empty;            // ISA05
    public string SenderId { get; set; } = string.Empty;                     // ISA06
    public string ReceiverIdQualifier { get; set; } = string.Empty;          // ISA07
    public string ReceiverId { get; set; } = string.Empty;                   // ISA08
    public string Date { get; set; } = string.Empty;                         // ISA09
    public string Time { get; set; } = string.Empty;                         // ISA10
    public string RepetitionSeparator { get; set; } = string.Empty;          // ISA11
    public string ControlVersionNumber { get; set; } = string.Empty;         // ISA12
    public string InterchangeControlNumber { get; set; } = string.Empty;     // ISA13
    public string AcknowledgmentRequested { get; set; } = string.Empty;      // ISA14
    public string UsageIndicator { get; set; } = string.Empty;               // ISA15 (P=Production, T=Test)
    public string ComponentSeparator { get; set; } = string.Empty;           // ISA16

    public static IsaSegment Parse(string[] elements)
    {
        return new IsaSegment
        {
            AuthorizationQualifier = elements.ElementAtOrDefault(1) ?? "",
            AuthorizationInformation = elements.ElementAtOrDefault(2) ?? "",
            SecurityQualifier = elements.ElementAtOrDefault(3) ?? "",
            SecurityInformation = elements.ElementAtOrDefault(4) ?? "",
            SenderIdQualifier = elements.ElementAtOrDefault(5) ?? "",
            SenderId = elements.ElementAtOrDefault(6)?.Trim() ?? "",
            ReceiverIdQualifier = elements.ElementAtOrDefault(7) ?? "",
            ReceiverId = elements.ElementAtOrDefault(8)?.Trim() ?? "",
            Date = elements.ElementAtOrDefault(9) ?? "",
            Time = elements.ElementAtOrDefault(10) ?? "",
            RepetitionSeparator = elements.ElementAtOrDefault(11) ?? "",
            ControlVersionNumber = elements.ElementAtOrDefault(12) ?? "",
            InterchangeControlNumber = elements.ElementAtOrDefault(13)?.Trim() ?? "",
            AcknowledgmentRequested = elements.ElementAtOrDefault(14) ?? "",
            UsageIndicator = elements.ElementAtOrDefault(15) ?? "",
            ComponentSeparator = elements.ElementAtOrDefault(16) ?? "",
        };
    }
}

/// <summary>
/// GS - Functional Group Header.
/// </summary>
[EdiSegment("GS")]
public sealed class GsSegment
{
    public string FunctionalIdentifierCode { get; set; } = string.Empty;     // GS01 (HC, HP, BE, FA, etc.)
    public string ApplicationSenderCode { get; set; } = string.Empty;        // GS02
    public string ApplicationReceiverCode { get; set; } = string.Empty;      // GS03
    public string Date { get; set; } = string.Empty;                         // GS04
    public string Time { get; set; } = string.Empty;                         // GS05
    public string GroupControlNumber { get; set; } = string.Empty;           // GS06
    public string ResponsibleAgencyCode { get; set; } = string.Empty;        // GS07
    public string VersionReleaseCode { get; set; } = string.Empty;           // GS08

    public static GsSegment Parse(string[] elements)
    {
        return new GsSegment
        {
            FunctionalIdentifierCode = elements.ElementAtOrDefault(1) ?? "",
            ApplicationSenderCode = elements.ElementAtOrDefault(2) ?? "",
            ApplicationReceiverCode = elements.ElementAtOrDefault(3) ?? "",
            Date = elements.ElementAtOrDefault(4) ?? "",
            Time = elements.ElementAtOrDefault(5) ?? "",
            GroupControlNumber = elements.ElementAtOrDefault(6) ?? "",
            ResponsibleAgencyCode = elements.ElementAtOrDefault(7) ?? "",
            VersionReleaseCode = elements.ElementAtOrDefault(8) ?? "",
        };
    }
}

/// <summary>
/// ST - Transaction Set Header.
/// </summary>
[EdiSegment("ST")]
public sealed class StSegment
{
    public string TransactionSetIdentifierCode { get; set; } = string.Empty; // ST01 (837, 835, etc.)
    public string TransactionSetControlNumber { get; set; } = string.Empty;  // ST02
    public string ImplementationConventionReference { get; set; } = string.Empty; // ST03

    public static StSegment Parse(string[] elements)
    {
        return new StSegment
        {
            TransactionSetIdentifierCode = elements.ElementAtOrDefault(1) ?? "",
            TransactionSetControlNumber = elements.ElementAtOrDefault(2) ?? "",
            ImplementationConventionReference = elements.ElementAtOrDefault(3) ?? "",
        };
    }
}
