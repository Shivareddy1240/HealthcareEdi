using HealthcareEdi.Core.Attributes;
using HealthcareEdi.Core.Parsing;
using HealthcareEdi.Core.Segments;

namespace HealthcareEdi.Transactions.ClaimStatus276277.Segments;

/// <summary>
/// STC - Status Information. The key response segment in a 277 providing claim status.
/// Contains status category, status code, and optional free-form status.
/// </summary>
[EdiSegment("STC")]
public class StcSegment : EdiSegmentBase
{
    /// <summary>STC01 - Composite: StatusCategoryCode:StatusCode:EntityIdentifier (e.g., A1:20:PR)</summary>
    public StatusInfo StatusInformation { get; set; } = new();
    public string StatusEffectiveDate { get; set; } = string.Empty;        // STC02
    public string ActionCode { get; set; } = string.Empty;                  // STC03
    public decimal TotalClaimChargeAmount { get; set; }                     // STC04
    public decimal ClaimPaymentAmount { get; set; }                         // STC05
    public string AdjudicationDate { get; set; } = string.Empty;           // STC06
    public string PaymentMethodCode { get; set; } = string.Empty;          // STC07
    public string CheckDate { get; set; } = string.Empty;                  // STC08
    public string CheckNumber { get; set; } = string.Empty;                // STC09

    // Additional STC01 composites (STC10, STC11 can carry additional status codes)
    public StatusInfo? StatusInformation2 { get; set; }
    public StatusInfo? StatusInformation3 { get; set; }

    public static StcSegment Parse(string[] elements, DelimiterContext delimiters)
    {
        var seg = new StcSegment { RawElements = elements };

        // STC01 - Composite status
        var stc01 = elements.ElementAtOrDefault(1) ?? "";
        if (!string.IsNullOrEmpty(stc01))
            seg.StatusInformation = StatusInfo.FromComposite(stc01, delimiters);

        seg.StatusEffectiveDate = elements.ElementAtOrDefault(2) ?? "";
        seg.ActionCode = elements.ElementAtOrDefault(3) ?? "";

        if (decimal.TryParse(elements.ElementAtOrDefault(4), out var charge))
            seg.TotalClaimChargeAmount = charge;
        if (decimal.TryParse(elements.ElementAtOrDefault(5), out var paid))
            seg.ClaimPaymentAmount = paid;

        seg.AdjudicationDate = elements.ElementAtOrDefault(6) ?? "";
        seg.PaymentMethodCode = elements.ElementAtOrDefault(7) ?? "";
        seg.CheckDate = elements.ElementAtOrDefault(8) ?? "";
        seg.CheckNumber = elements.ElementAtOrDefault(9) ?? "";

        // STC10 - Second status composite
        var stc10 = elements.ElementAtOrDefault(10) ?? "";
        if (!string.IsNullOrEmpty(stc10))
            seg.StatusInformation2 = StatusInfo.FromComposite(stc10, delimiters);

        // STC11 - Third status composite
        var stc11 = elements.ElementAtOrDefault(11) ?? "";
        if (!string.IsNullOrEmpty(stc11))
            seg.StatusInformation3 = StatusInfo.FromComposite(stc11, delimiters);

        return seg;
    }
}

/// <summary>
/// Strongly-typed status information from STC composite elements.
/// </summary>
public class StatusInfo
{
    /// <summary>Health Care Claim Status Category Code (e.g., A0=Acknowledgement, A1=Certification, F1=Finalized)</summary>
    public string CategoryCode { get; set; } = string.Empty;

    /// <summary>Claim Status Code (e.g., 0=Cannot provide status, 1=For review, 2=Suspended, 3=Pending, 15=In adjudication, 19=Processed, 20=Accepted, 21=Rejected)</summary>
    public string StatusCode { get; set; } = string.Empty;

    /// <summary>Entity Identifier Code (e.g., PR=Payer, AE=Physician)</summary>
    public string EntityIdentifierCode { get; set; } = string.Empty;

    /// <summary>Readable category description.</summary>
    public string CategoryDescription => CategoryCode switch
    {
        "A0" => "Acknowledgement/Receipt",
        "A1" => "Certification/Recertification",
        "A2" => "Added/Appended",
        "A3" => "Accepted",
        "A4" => "Additional Information Requested",
        "DR" => "In Process/Adjudication",
        "E0" => "Response Not Possible",
        "F0" => "Finalized",
        "F1" => "Finalized/Payment",
        "F2" => "Finalized/Denial",
        "F3" => "Finalized/Revised",
        "F4" => "Finalized/Forwarded",
        "P0" => "Pending - Payer Review",
        "P1" => "Pending - Provider Requested Info",
        "P2" => "Pending - Payer Administrative",
        "P3" => "Pending - Patient Requested",
        "P4" => "Pending - Risk Adjustment",
        "R0" => "Requests for Additional Info",
        "RQ" => "Request Rejected",
        _ => $"Category {CategoryCode}"
    };

    /// <summary>True if claim is finalized (F0-F4).</summary>
    public bool IsFinalized => CategoryCode.StartsWith("F");

    /// <summary>True if claim is pending (P0-P4).</summary>
    public bool IsPending => CategoryCode.StartsWith("P");

    /// <summary>True if claim was denied (F2).</summary>
    public bool IsDenied => CategoryCode == "F2";

    /// <summary>True if claim was paid (F1).</summary>
    public bool IsPaid => CategoryCode == "F1";

    public static StatusInfo FromComposite(string composite, DelimiterContext delimiters)
    {
        var components = delimiters.SplitComponents(composite);
        return new StatusInfo
        {
            CategoryCode = components.ElementAtOrDefault(0) ?? "",
            StatusCode = components.ElementAtOrDefault(1) ?? "",
            EntityIdentifierCode = components.ElementAtOrDefault(2) ?? "",
        };
    }

    public override string ToString() => $"{CategoryCode}:{StatusCode}" +
        (!string.IsNullOrEmpty(EntityIdentifierCode) ? $":{EntityIdentifierCode}" : "");
}
