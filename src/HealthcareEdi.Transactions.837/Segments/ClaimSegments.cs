using HealthcareEdi.Core.Attributes;
using HealthcareEdi.Core.Parsing;
using HealthcareEdi.Core.Segments;

namespace HealthcareEdi.Transactions.Claim837.Segments;

/// <summary>
/// CLM - Claim Information. The primary claim-level segment.
/// </summary>
[EdiSegment("CLM")]
public class ClmSegment : EdiSegmentBase
{
    public string PatientAccountNumber { get; set; } = string.Empty;    // CLM01
    public decimal TotalClaimChargeAmount { get; set; }                  // CLM02
    public string FacilityCodeValue { get; set; } = string.Empty;       // CLM05-1 (composite)
    public string FacilityCodeQualifier { get; set; } = string.Empty;   // CLM05-2
    public string ClaimFrequencyCode { get; set; } = string.Empty;      // CLM05-3
    public string ProviderSignatureIndicator { get; set; } = string.Empty; // CLM06
    public string ProviderAcceptAssignment { get; set; } = string.Empty;   // CLM07
    public string BenefitsAssignmentCert { get; set; } = string.Empty;     // CLM08
    public string ReleaseOfInfoCode { get; set; } = string.Empty;          // CLM09

    public static ClmSegment Parse(string[] elements, DelimiterContext delimiters)
    {
        var segment = new ClmSegment
        {
            RawElements = elements,
            PatientAccountNumber = elements.ElementAtOrDefault(1) ?? "",
            ProviderSignatureIndicator = elements.ElementAtOrDefault(6) ?? "",
            ProviderAcceptAssignment = elements.ElementAtOrDefault(7) ?? "",
            BenefitsAssignmentCert = elements.ElementAtOrDefault(8) ?? "",
            ReleaseOfInfoCode = elements.ElementAtOrDefault(9) ?? "",
        };

        // CLM02 - Total charge
        if (decimal.TryParse(elements.ElementAtOrDefault(2), out var charge))
            segment.TotalClaimChargeAmount = charge;

        // CLM05 - Composite: FacilityCode:Qualifier:FrequencyCode
        var clm05 = elements.ElementAtOrDefault(5) ?? "";
        if (!string.IsNullOrEmpty(clm05))
        {
            var components = delimiters.SplitComponents(clm05);
            segment.FacilityCodeValue = components.ElementAtOrDefault(0) ?? "";
            segment.FacilityCodeQualifier = components.ElementAtOrDefault(1) ?? "";
            segment.ClaimFrequencyCode = components.ElementAtOrDefault(2) ?? "";
        }

        return segment;
    }
}

/// <summary>
/// HI - Health Care Information Codes (diagnosis codes).
/// Each element is a composite: Qualifier:Code (e.g., ABK:J441).
/// </summary>
[EdiSegment("HI")]
public class HiSegment : EdiSegmentBase
{
    public List<DiagnosisCode> DiagnosisCodes { get; set; } = [];

    public static HiSegment Parse(string[] elements, DelimiterContext delimiters)
    {
        var segment = new HiSegment { RawElements = elements };

        // HI01 through HI12 - each is a composite
        for (int i = 1; i < elements.Length && i <= 12; i++)
        {
            var element = elements[i];
            if (string.IsNullOrEmpty(element)) continue;

            var components = delimiters.SplitComponents(element);
            segment.DiagnosisCodes.Add(new DiagnosisCode
            {
                Qualifier = components.ElementAtOrDefault(0) ?? "",
                Code = components.ElementAtOrDefault(1) ?? "",
            });
        }

        return segment;
    }
}

/// <summary>
/// Strongly-typed diagnosis code from HI composite elements.
/// </summary>
public class DiagnosisCode
{
    /// <summary>ABK=Principal ICD-10, ABF=ICD-10, ABJ=ICD-10 Admitting, BK=Principal ICD-9</summary>
    public string Qualifier { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    public bool IsPrincipal => Qualifier is "ABK" or "BK";
    public override string ToString() => $"{Qualifier}:{Code}";
}

/// <summary>
/// SV1 - Professional Service (837P).
/// </summary>
[EdiSegment("SV1")]
public class Sv1Segment : EdiSegmentBase
{
    public ProcedureCode Procedure { get; set; } = new();
    public decimal ChargeAmount { get; set; }               // SV102
    public string UnitBasisCode { get; set; } = string.Empty; // SV103 (UN=Unit)
    public decimal ServiceUnitCount { get; set; }             // SV104
    public string PlaceOfServiceCode { get; set; } = string.Empty; // SV105
    public string DiagnosisCodePointer { get; set; } = string.Empty; // SV107

    public static Sv1Segment Parse(string[] elements, DelimiterContext delimiters)
    {
        var segment = new Sv1Segment { RawElements = elements };

        // SV101 - Composite: CodeType:Code:Modifier1:Modifier2:Modifier3:Modifier4
        var sv101 = elements.ElementAtOrDefault(1) ?? "";
        if (!string.IsNullOrEmpty(sv101))
        {
            var components = delimiters.SplitComponents(sv101);
            segment.Procedure = new ProcedureCode
            {
                CodeType = components.ElementAtOrDefault(0) ?? "",
                Code = components.ElementAtOrDefault(1) ?? "",
                Modifier1 = components.ElementAtOrDefault(2) ?? "",
                Modifier2 = components.ElementAtOrDefault(3) ?? "",
                Modifier3 = components.ElementAtOrDefault(4) ?? "",
                Modifier4 = components.ElementAtOrDefault(5) ?? "",
            };
        }

        if (decimal.TryParse(elements.ElementAtOrDefault(2), out var charge))
            segment.ChargeAmount = charge;
        segment.UnitBasisCode = elements.ElementAtOrDefault(3) ?? "";
        if (decimal.TryParse(elements.ElementAtOrDefault(4), out var units))
            segment.ServiceUnitCount = units;
        segment.PlaceOfServiceCode = elements.ElementAtOrDefault(5) ?? "";
        segment.DiagnosisCodePointer = elements.ElementAtOrDefault(7) ?? "";

        return segment;
    }
}

/// <summary>
/// SV2 - Institutional Service Line (837I).
/// </summary>
[EdiSegment("SV2")]
public class Sv2Segment : EdiSegmentBase
{
    public string RevenueCode { get; set; } = string.Empty;  // SV201
    public ProcedureCode Procedure { get; set; } = new();     // SV202 (composite)
    public decimal ChargeAmount { get; set; }                  // SV203
    public string UnitBasisCode { get; set; } = string.Empty;  // SV204
    public decimal ServiceUnitCount { get; set; }              // SV205

    public static Sv2Segment Parse(string[] elements, DelimiterContext delimiters)
    {
        var segment = new Sv2Segment
        {
            RawElements = elements,
            RevenueCode = elements.ElementAtOrDefault(1) ?? "",
        };

        var sv202 = elements.ElementAtOrDefault(2) ?? "";
        if (!string.IsNullOrEmpty(sv202))
        {
            var components = delimiters.SplitComponents(sv202);
            segment.Procedure = new ProcedureCode
            {
                CodeType = components.ElementAtOrDefault(0) ?? "",
                Code = components.ElementAtOrDefault(1) ?? "",
                Modifier1 = components.ElementAtOrDefault(2) ?? "",
                Modifier2 = components.ElementAtOrDefault(3) ?? "",
                Modifier3 = components.ElementAtOrDefault(4) ?? "",
                Modifier4 = components.ElementAtOrDefault(5) ?? "",
            };
        }

        if (decimal.TryParse(elements.ElementAtOrDefault(3), out var charge))
            segment.ChargeAmount = charge;
        segment.UnitBasisCode = elements.ElementAtOrDefault(4) ?? "";
        if (decimal.TryParse(elements.ElementAtOrDefault(5), out var units))
            segment.ServiceUnitCount = units;

        return segment;
    }
}

/// <summary>
/// SV3 - Dental Service Line (837D).
/// </summary>
[EdiSegment("SV3")]
public class Sv3Segment : EdiSegmentBase
{
    public ProcedureCode Procedure { get; set; } = new();     // SV301 (composite)
    public decimal ChargeAmount { get; set; }                  // SV302
    public string PlaceOfServiceCode { get; set; } = string.Empty; // SV303

    public static Sv3Segment Parse(string[] elements, DelimiterContext delimiters)
    {
        var segment = new Sv3Segment { RawElements = elements };

        var sv301 = elements.ElementAtOrDefault(1) ?? "";
        if (!string.IsNullOrEmpty(sv301))
        {
            var components = delimiters.SplitComponents(sv301);
            segment.Procedure = new ProcedureCode
            {
                CodeType = components.ElementAtOrDefault(0) ?? "",
                Code = components.ElementAtOrDefault(1) ?? "",
                Modifier1 = components.ElementAtOrDefault(2) ?? "",
                Modifier2 = components.ElementAtOrDefault(3) ?? "",
                Modifier3 = components.ElementAtOrDefault(4) ?? "",
                Modifier4 = components.ElementAtOrDefault(5) ?? "",
            };
        }

        if (decimal.TryParse(elements.ElementAtOrDefault(2), out var charge))
            segment.ChargeAmount = charge;
        segment.PlaceOfServiceCode = elements.ElementAtOrDefault(3) ?? "";

        return segment;
    }
}

/// <summary>
/// Strongly-typed procedure code from SV1/SV2/SV3 composite elements.
/// </summary>
public class ProcedureCode
{
    /// <summary>HC=HCPCS, IV=ICD-10-PCS, ZZ=Mutually Defined</summary>
    public string CodeType { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Modifier1 { get; set; } = string.Empty;
    public string Modifier2 { get; set; } = string.Empty;
    public string Modifier3 { get; set; } = string.Empty;
    public string Modifier4 { get; set; } = string.Empty;

    public string[] Modifiers => new[] { Modifier1, Modifier2, Modifier3, Modifier4 }
        .Where(m => !string.IsNullOrEmpty(m)).ToArray();

    public override string ToString() => $"{CodeType}:{Code}" +
        (Modifiers.Length > 0 ? $":{string.Join(":", Modifiers)}" : "");
}

/// <summary>
/// CL1 - Institutional Claim Code (837I only).
/// </summary>
[EdiSegment("CL1")]
public class Cl1Segment : EdiSegmentBase
{
    public string AdmissionTypeCode { get; set; } = string.Empty;    // CL101
    public string AdmissionSourceCode { get; set; } = string.Empty;  // CL102
    public string PatientStatusCode { get; set; } = string.Empty;    // CL103

    public static Cl1Segment Parse(string[] elements)
    {
        return new Cl1Segment
        {
            RawElements = elements,
            AdmissionTypeCode = elements.ElementAtOrDefault(1) ?? "",
            AdmissionSourceCode = elements.ElementAtOrDefault(2) ?? "",
            PatientStatusCode = elements.ElementAtOrDefault(3) ?? "",
        };
    }
}
