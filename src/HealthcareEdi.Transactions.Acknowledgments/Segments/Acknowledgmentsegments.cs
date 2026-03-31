using HealthcareEdi.Core.Attributes;
using HealthcareEdi.Core.Envelopes;
using HealthcareEdi.Core.Models.Base;
using HealthcareEdi.Core.Parsing;
using HealthcareEdi.Core.Segments;
using HealthcareEdi.Core.Validation;
using System.Diagnostics;

namespace HealthcareEdi.Transactions.Acknowledgments.Segments;

/// <summary>AK1 - Functional Group Response Header. Identifies the group being acknowledged.</summary>
[EdiSegment("AK1")]
public class Ak1Segment : EdiSegmentBase
{
    public string FunctionalIdentifierCode { get; set; } = string.Empty;  // AK101 (HC, HP, BE, etc.)
    public string GroupControlNumber { get; set; } = string.Empty;        // AK102
    public string VersionReleaseCode { get; set; } = string.Empty;       // AK103

    public static Ak1Segment Parse(string[] elements) => new()
    {
        RawElements = elements,
        FunctionalIdentifierCode = elements.ElementAtOrDefault(1) ?? "",
        GroupControlNumber = elements.ElementAtOrDefault(2) ?? "",
        VersionReleaseCode = elements.ElementAtOrDefault(3) ?? "",
    };
}

/// <summary>AK2 - Transaction Set Response Header. Identifies a specific transaction set.</summary>
[EdiSegment("AK2")]
public class Ak2Segment : EdiSegmentBase
{
    public string TransactionSetIdentifierCode { get; set; } = string.Empty; // AK201
    public string TransactionSetControlNumber { get; set; } = string.Empty;  // AK202
    public string ImplementationConventionReference { get; set; } = string.Empty; // AK203

    public static Ak2Segment Parse(string[] elements) => new()
    {
        RawElements = elements,
        TransactionSetIdentifierCode = elements.ElementAtOrDefault(1) ?? "",
        TransactionSetControlNumber = elements.ElementAtOrDefault(2) ?? "",
        ImplementationConventionReference = elements.ElementAtOrDefault(3) ?? "",
    };
}

/// <summary>IK5 (999) / AK5 (997) - Transaction Set Response Trailer.</summary>
[EdiSegment("IK5")]
public class Ik5Segment : EdiSegmentBase
{
    public string TransactionSetAcknowledgmentCode { get; set; } = string.Empty; // IK501 (A=Accepted, E=Accepted with Errors, R=Rejected)
    public string ErrorCode1 { get; set; } = string.Empty; // IK502
    public string ErrorCode2 { get; set; } = string.Empty; // IK503

    public bool IsAccepted => TransactionSetAcknowledgmentCode == "A";
    public bool IsAcceptedWithErrors => TransactionSetAcknowledgmentCode == "E";
    public bool IsRejected => TransactionSetAcknowledgmentCode == "R";

    public string AcknowledgmentDescription => TransactionSetAcknowledgmentCode switch
    {
        "A" => "Accepted",
        "E" => "Accepted with Errors",
        "M" => "Rejected - Message Auth Code Failed",
        "P" => "Partially Accepted",
        "R" => "Rejected",
        "W" => "Rejected - Failed Validity Tests",
        "X" => "Rejected - Decryption Not Possible",
        _ => $"Code {TransactionSetAcknowledgmentCode}"
    };

    public static Ik5Segment Parse(string[] elements) => new()
    {
        RawElements = elements,
        TransactionSetAcknowledgmentCode = elements.ElementAtOrDefault(1) ?? "",
        ErrorCode1 = elements.ElementAtOrDefault(2) ?? "",
        ErrorCode2 = elements.ElementAtOrDefault(3) ?? "",
    };
}

/// <summary>AK9 (999/997) - Functional Group Response Trailer.</summary>
[EdiSegment("AK9")]
public class Ak9Segment : EdiSegmentBase
{
    public string FunctionalGroupAcknowledgmentCode { get; set; } = string.Empty; // AK901 (A/E/P/R)
    public int NumberOfTransactionSetsIncluded { get; set; }  // AK902
    public int NumberOfTransactionSetsReceived { get; set; }  // AK903
    public int NumberOfTransactionSetsAccepted { get; set; }  // AK904
    public string ErrorCode1 { get; set; } = string.Empty;    // AK905

    public bool IsAccepted => FunctionalGroupAcknowledgmentCode == "A";
    public bool IsRejected => FunctionalGroupAcknowledgmentCode == "R";
    public bool IsPartiallyAccepted => FunctionalGroupAcknowledgmentCode == "P";

    public static Ak9Segment Parse(string[] elements)
    {
        var seg = new Ak9Segment
        {
            RawElements = elements,
            FunctionalGroupAcknowledgmentCode = elements.ElementAtOrDefault(1) ?? "",
            ErrorCode1 = elements.ElementAtOrDefault(5) ?? "",
        };
        if (int.TryParse(elements.ElementAtOrDefault(2), out var inc)) seg.NumberOfTransactionSetsIncluded = inc;
        if (int.TryParse(elements.ElementAtOrDefault(3), out var rec)) seg.NumberOfTransactionSetsReceived = rec;
        if (int.TryParse(elements.ElementAtOrDefault(4), out var acc)) seg.NumberOfTransactionSetsAccepted = acc;
        return seg;
    }
}

/// <summary>TA1 - Interchange Acknowledgment. Standalone segment (not inside ST/SE).</summary>
[EdiSegment("TA1")]
public class Ta1Segment : EdiSegmentBase
{
    public string InterchangeControlNumber { get; set; } = string.Empty;   // TA101
    public string InterchangeDate { get; set; } = string.Empty;            // TA102
    public string InterchangeTime { get; set; } = string.Empty;            // TA103
    public string AcknowledgmentCode { get; set; } = string.Empty;         // TA104 (A=Accepted, E=Accepted with Errors, R=Rejected)
    public string NoteCode { get; set; } = string.Empty;                   // TA105

    public bool IsAccepted => AcknowledgmentCode == "A";
    public bool IsRejected => AcknowledgmentCode == "R";

    public static Ta1Segment Parse(string[] elements) => new()
    {
        RawElements = elements,
        InterchangeControlNumber = elements.ElementAtOrDefault(1) ?? "",
        InterchangeDate = elements.ElementAtOrDefault(2) ?? "",
        InterchangeTime = elements.ElementAtOrDefault(3) ?? "",
        AcknowledgmentCode = elements.ElementAtOrDefault(4) ?? "",
        NoteCode = elements.ElementAtOrDefault(5) ?? "",
    };
}
