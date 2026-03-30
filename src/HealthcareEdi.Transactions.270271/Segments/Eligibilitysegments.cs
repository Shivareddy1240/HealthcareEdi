using HealthcareEdi.Core.Attributes;
using HealthcareEdi.Core.Parsing;
using HealthcareEdi.Core.Segments;

namespace HealthcareEdi.Transactions.Eligibility270271.Segments;

/// <summary>
/// EB - Eligibility or Benefit Information. The primary response segment in a 271.
/// Contains coverage status, service types, amounts, percentages, and plan details.
/// </summary>
[EdiSegment("EB")]
public class EbSegment : EdiSegmentBase
{
    public string EligibilityOrBenefitCode { get; set; } = string.Empty;   // EB01 (1=Active, 6=Inactive, I=Non-Covered, etc.)
    public string CoverageLevelCode { get; set; } = string.Empty;          // EB02 (IND=Individual, FAM=Family, EMP=Employee, etc.)
    public string ServiceTypeCode { get; set; } = string.Empty;            // EB03 (30=Health Plan, 1=Medical Care, 35=Dental, 47=Hospital, etc.)
    public string InsuranceTypeCode { get; set; } = string.Empty;          // EB04 (HM=HMO, PP=PPO, PR=Preferred Provider, etc.)
    public string PlanCoverageDescription { get; set; } = string.Empty;    // EB05
    public string TimePeriodQualifier { get; set; } = string.Empty;        // EB06 (6=Hour, 7=Day, 21=Years, 22=Service Year, 23=Calendar Year, 24=Year to Date, 25=Contract, 26=Episode, 27=Visit, 29=Lifetime, 32=Remaining, 33=Exceeded)
    public decimal MonetaryAmount { get; set; }                             // EB07
    public decimal Percent { get; set; }                                    // EB08 (e.g., 0.20 = 20% coinsurance)
    public string QuantityQualifier { get; set; } = string.Empty;          // EB09
    public decimal Quantity { get; set; }                                    // EB10
    public string AuthorizationRequired { get; set; } = string.Empty;      // EB11 (Y/N)
    public string InPlanNetworkIndicator { get; set; } = string.Empty;     // EB12 (Y=In-Network, N=Out-of-Network, W=Not Applicable)

    // ── Computed Properties ─────────────────────────────────────

    /// <summary>True if member is actively covered.</summary>
    public bool IsActive => EligibilityOrBenefitCode == "1";

    /// <summary>True if this is a co-pay amount.</summary>
    public bool IsCopay => EligibilityOrBenefitCode == "B";

    /// <summary>True if this is a deductible amount.</summary>
    public bool IsDeductible => EligibilityOrBenefitCode == "C";

    /// <summary>True if this is a coinsurance percentage.</summary>
    public bool IsCoinsurance => EligibilityOrBenefitCode == "A";

    /// <summary>True if this is an out-of-pocket maximum.</summary>
    public bool IsOutOfPocket => EligibilityOrBenefitCode == "G";

    /// <summary>True if service is not covered.</summary>
    public bool IsNonCovered => EligibilityOrBenefitCode == "I";

    /// <summary>True if benefit applies to in-network providers.</summary>
    public bool IsInNetwork => InPlanNetworkIndicator == "Y";

    /// <summary>True if benefit applies to out-of-network providers.</summary>
    public bool IsOutOfNetwork => InPlanNetworkIndicator == "N";

    /// <summary>Readable eligibility/benefit description.</summary>
    public string BenefitDescription => EligibilityOrBenefitCode switch
    {
        "1" => "Active Coverage",
        "2" => "Active - Full Risk Capitation",
        "3" => "Active - Services Capitated",
        "4" => "Active - Services Capitated to Primary Care Physician",
        "5" => "Active - Pending Investigation",
        "6" => "Inactive",
        "7" => "Inactive - Pending Eligibility Update",
        "8" => "Inactive - Pending Investigation",
        "A" => "Co-Insurance",
        "B" => "Co-Payment",
        "C" => "Deductible",
        "CB" => "Coverage Basis",
        "D" => "Benefit Description",
        "E" => "Exclusions",
        "F" => "Limitations",
        "G" => "Out of Pocket (Stop Loss)",
        "H" => "Unlimited",
        "I" => "Non-Covered",
        "J" => "Cost Containment",
        "K" => "Reserve",
        "L" => "Primary Care Provider",
        "MC" => "Managed Care Coordinator",
        "N" => "Services Restricted to Following Provider",
        "R" => "Other or Additional Payor",
        "S" => "Spend Down",
        "T" => "Contribution Amount",
        "U" => "Maximum",
        "V" => "Cannot Process",
        "W" => "Other Source of Data",
        "X" => "Health Care Facility",
        "Y" => "Spend Down",
        _ => $"Code {EligibilityOrBenefitCode}"
    };

    /// <summary>Readable service type description.</summary>
    public string ServiceTypeDescription => ServiceTypeCode switch
    {
        "1" => "Medical Care",
        "2" => "Surgical",
        "3" => "Consultation",
        "4" => "Diagnostic X-Ray",
        "5" => "Diagnostic Lab",
        "6" => "Radiation Therapy",
        "7" => "Anesthesia",
        "8" => "Surgical Assistance",
        "12" => "Durable Medical Equipment",
        "14" => "Renal Supplies in the Home",
        "23" => "Diagnostic Dental",
        "24" => "Periodontics",
        "25" => "Prosthodontics",
        "26" => "Oral Surgery",
        "27" => "Orthodontics",
        "30" => "Health Benefit Plan Coverage",
        "33" => "Chiropractic",
        "35" => "Dental Care",
        "36" => "Vision (Optometry)",
        "37" => "Vision (Optician)",
        "38" => "Hearing",
        "39" => "Pneumonia Vaccine",
        "41" => "Routine Preventive Dental",
        "42" => "Home Health Care",
        "45" => "Hospice",
        "47" => "Hospital",
        "48" => "Hospital - Inpatient",
        "50" => "Hospital - Outpatient",
        "51" => "Hospital - Emergency Accident",
        "52" => "Hospital - Emergency Medical",
        "53" => "Hospital - Ambulatory Surgical",
        "54" => "Long-Term Care",
        "55" => "Major Medical",
        "56" => "Medically Related Transportation",
        "60" => "General Benefits",
        "61" => "In-vitro Fertilization",
        "62" => "MRI/CAT Scan",
        "65" => "Newborn Care",
        "67" => "Smoking Cessation",
        "68" => "Well Baby Care",
        "69" => "Maternity",
        "70" => "Transplants",
        "71" => "Audiological Exam",
        "72" => "Inhalation Therapy",
        "73" => "Diagnostic Medical",
        "76" => "Dialysis",
        "82" => "Chemotherapy",
        "83" => "Allergy Testing",
        "84" => "Immunizations",
        "86" => "Emergency Services",
        "88" => "Pharmacy",
        "98" => "Professional (Physician) Visit - Office",
        "AL" => "Vision",
        "MH" => "Mental Health",
        "UC" => "Urgent Care",
        _ => $"Service Type {ServiceTypeCode}"
    };

    /// <summary>Readable time period description.</summary>
    public string TimePeriodDescription => TimePeriodQualifier switch
    {
        "6" => "Hour",
        "7" => "Day",
        "21" => "Years",
        "22" => "Service Year",
        "23" => "Calendar Year",
        "24" => "Year to Date",
        "25" => "Contract",
        "26" => "Episode",
        "27" => "Visit",
        "29" => "Lifetime",
        "32" => "Remaining",
        "33" => "Exceeded",
        _ => TimePeriodQualifier
    };

    public static EbSegment Parse(string[] elements)
    {
        var seg = new EbSegment
        {
            RawElements = elements,
            EligibilityOrBenefitCode = elements.ElementAtOrDefault(1) ?? "",
            CoverageLevelCode = elements.ElementAtOrDefault(2) ?? "",
            ServiceTypeCode = elements.ElementAtOrDefault(3) ?? "",
            InsuranceTypeCode = elements.ElementAtOrDefault(4) ?? "",
            PlanCoverageDescription = elements.ElementAtOrDefault(5) ?? "",
            TimePeriodQualifier = elements.ElementAtOrDefault(6) ?? "",
            QuantityQualifier = elements.ElementAtOrDefault(9) ?? "",
            AuthorizationRequired = elements.ElementAtOrDefault(11) ?? "",
            InPlanNetworkIndicator = elements.ElementAtOrDefault(12) ?? "",
        };

        if (decimal.TryParse(elements.ElementAtOrDefault(7), out var amount))
            seg.MonetaryAmount = amount;
        if (decimal.TryParse(elements.ElementAtOrDefault(8), out var pct))
            seg.Percent = pct;
        if (decimal.TryParse(elements.ElementAtOrDefault(10), out var qty))
            seg.Quantity = qty;

        return seg;
    }
}

/// <summary>
/// EQ - Eligibility or Benefit Inquiry Information. Used in 270 to specify what to inquire about.
/// </summary>
[EdiSegment("EQ")]
public class EqSegment : EdiSegmentBase
{
    public string ServiceTypeCode { get; set; } = string.Empty;            // EQ01 (30=Health Plan, 1=Medical, 35=Dental, etc.)
    public string CompositeMedicalProcedureId { get; set; } = string.Empty; // EQ02 (composite - procedure code)
    public string CoverageLevelCode { get; set; } = string.Empty;          // EQ03

    public static EqSegment Parse(string[] elements)
    {
        return new EqSegment
        {
            RawElements = elements,
            ServiceTypeCode = elements.ElementAtOrDefault(1) ?? "",
            CompositeMedicalProcedureId = elements.ElementAtOrDefault(2) ?? "",
            CoverageLevelCode = elements.ElementAtOrDefault(3) ?? "",
        };
    }
}

/// <summary>
/// MSG - Message Text. Free-form text providing additional benefit information.
/// </summary>
[EdiSegment("MSG")]
public class MsgSegment : EdiSegmentBase
{
    public string FreeFormMessageText { get; set; } = string.Empty; // MSG01

    public static MsgSegment Parse(string[] elements)
    {
        return new MsgSegment
        {
            RawElements = elements,
            FreeFormMessageText = elements.ElementAtOrDefault(1) ?? "",
        };
    }
}

/// <summary>
/// III - Additional Information. Provides coded additional eligibility details.
/// </summary>
[EdiSegment("III")]
public class IiiSegment : EdiSegmentBase
{
    public string CodeListQualifier { get; set; } = string.Empty;  // III01
    public string IndustryCode { get; set; } = string.Empty;       // III02

    public static IiiSegment Parse(string[] elements)
    {
        return new IiiSegment
        {
            RawElements = elements,
            CodeListQualifier = elements.ElementAtOrDefault(1) ?? "",
            IndustryCode = elements.ElementAtOrDefault(2) ?? "",
        };
    }
}
