using HealthcareEdi.Core.Attributes;
using HealthcareEdi.Core.Parsing;
using HealthcareEdi.Core.Segments;

namespace HealthcareEdi.Transactions.PriorAuth278.Segments;

/// <summary>
/// UM - Health Care Services Review Information. Core segment for prior auth requests.
/// </summary>
[EdiSegment("UM")]
public class UmSegment : EdiSegmentBase
{
    public string RequestCategoryCode { get; set; } = string.Empty;       // UM01 (AR=Admission Review, HS=Health Services Review, SC=Specialty Care Review)
    public string CertificationTypeCode { get; set; } = string.Empty;    // UM02 (I=Initial, R=Renewal, E=Extension)
    public string ServiceTypeCode { get; set; } = string.Empty;           // UM03
    public string FacilityCodeValue { get; set; } = string.Empty;         // UM04-1 (composite)
    public string FacilityCodeQualifier { get; set; } = string.Empty;     // UM04-2
    public string LevelOfServiceCode { get; set; } = string.Empty;        // UM05
    public string CurrentHealthConditionCode { get; set; } = string.Empty; // UM06

    public string RequestCategory => RequestCategoryCode switch
    {
        "AR" => "Admission Review",
        "HS" => "Health Services Review",
        "SC" => "Specialty Care Review",
        _ => RequestCategoryCode
    };

    public string CertificationType => CertificationTypeCode switch
    {
        "I" => "Initial",
        "R" => "Renewal",
        "E" => "Extension",
        "S" => "Concurrent Review",
        _ => CertificationTypeCode
    };

    public static UmSegment Parse(string[] elements, DelimiterContext delimiters)
    {
        var seg = new UmSegment
        {
            RawElements = elements,
            RequestCategoryCode = elements.ElementAtOrDefault(1) ?? "",
            CertificationTypeCode = elements.ElementAtOrDefault(2) ?? "",
            ServiceTypeCode = elements.ElementAtOrDefault(3) ?? "",
            LevelOfServiceCode = elements.ElementAtOrDefault(5) ?? "",
            CurrentHealthConditionCode = elements.ElementAtOrDefault(6) ?? "",
        };

        var um04 = elements.ElementAtOrDefault(4) ?? "";
        if (!string.IsNullOrEmpty(um04))
        {
            var components = delimiters.SplitComponents(um04);
            seg.FacilityCodeValue = components.ElementAtOrDefault(0) ?? "";
            seg.FacilityCodeQualifier = components.ElementAtOrDefault(1) ?? "";
        }
        return seg;
    }
}

/// <summary>
/// HCR - Health Care Services Review. Contains the payer's decision on the auth request (278 response).
/// </summary>
[EdiSegment("HCR")]
public class HcrSegment : EdiSegmentBase
{
    public string ActionCode { get; set; } = string.Empty;               // HCR01 (A1=Certified, A2=Certified with Changes, A3=Not Certified, A4=Pend, A6=Modified)
    public string ReviewIdentificationNumber { get; set; } = string.Empty; // HCR02 (Auth/cert number)

    public bool IsCertified => ActionCode is "A1" or "A2" or "A6";
    public bool IsDenied => ActionCode == "A3";
    public bool IsPended => ActionCode == "A4";

    public string Decision => ActionCode switch
    {
        "A1" => "Certified in Total",
        "A2" => "Certified with Changes",
        "A3" => "Not Certified / Denied",
        "A4" => "Pended",
        "A6" => "Modified",
        "CT" => "Contact Payer",
        "NA" => "No Action Required",
        _ => $"Action {ActionCode}"
    };

    public static HcrSegment Parse(string[] elements)
    {
        return new HcrSegment
        {
            RawElements = elements,
            ActionCode = elements.ElementAtOrDefault(1) ?? "",
            ReviewIdentificationNumber = elements.ElementAtOrDefault(2) ?? "",
        };
    }
}
