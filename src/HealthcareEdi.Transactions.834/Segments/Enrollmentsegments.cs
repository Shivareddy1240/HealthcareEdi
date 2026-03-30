using HealthcareEdi.Core.Attributes;
using HealthcareEdi.Core.Segments;

namespace HealthcareEdi.Transactions.Enrollment834.Segments;

/// <summary>
/// INS - Insured Benefit. The anchor segment for each member in an 834.
/// Determines whether this is an add, change, reinstatement, or termination.
/// </summary>
[EdiSegment("INS")]
public class InsSegment : EdiSegmentBase
{
    public string YesNoResponseCode { get; set; } = string.Empty;          // INS01 (Y=Subscriber, N=Dependent)
    public string RelationshipCode { get; set; } = string.Empty;           // INS02 (18=Self, 01=Spouse, 19=Child, etc.)
    public string MaintenanceTypeCode { get; set; } = string.Empty;        // INS03 (001=Change, 021=Add, 024=Cancel/Term, 025=Reinstate)
    public string MaintenanceReasonCode { get; set; } = string.Empty;      // INS04 (AI=Active, EC=Election Change, XN=Notification Only)
    public string BenefitStatusCode { get; set; } = string.Empty;          // INS05 (A=Active, C=COBRA, S=Surviving Insured, T=TEFRA)
    public string MedicarePlanCode { get; set; } = string.Empty;           // INS06
    public string ConsolidatedOmnibusBudgetReconciliationAct { get; set; } = string.Empty; // INS07
    public string EmploymentStatusCode { get; set; } = string.Empty;       // INS08 (AC=Active, AO=Active Military-Overseas, FT=Full-Time, RT=Retired)
    public string StudentStatusCode { get; set; } = string.Empty;          // INS09 (F=Full-Time, N=Not a Student, P=Part-Time)
    public string HandicapIndicator { get; set; } = string.Empty;          // INS10 (Y/N)
    public string DateTimePeriodFormatQualifier { get; set; } = string.Empty; // INS11
    public string DateTimePeriod { get; set; } = string.Empty;             // INS12 (Death date if applicable)

    /// <summary>True if this is the subscriber (not a dependent).</summary>
    public bool IsSubscriber => YesNoResponseCode == "Y";

    /// <summary>True if this is an enrollment add.</summary>
    public bool IsAdd => MaintenanceTypeCode == "021";

    /// <summary>True if this is a termination/cancellation.</summary>
    public bool IsTermination => MaintenanceTypeCode == "024";

    /// <summary>True if this is a change to existing enrollment.</summary>
    public bool IsChange => MaintenanceTypeCode == "001";

    /// <summary>True if this is a reinstatement.</summary>
    public bool IsReinstatement => MaintenanceTypeCode == "025";

    /// <summary>Readable description of the maintenance action.</summary>
    public string MaintenanceAction => MaintenanceTypeCode switch
    {
        "001" => "Change",
        "021" => "Addition",
        "024" => "Cancellation/Termination",
        "025" => "Reinstatement",
        "026" => "Audit/Compare",
        "030" => "Employee Information Not Applicable",
        _ => $"Unknown ({MaintenanceTypeCode})"
    };

    /// <summary>Readable relationship description.</summary>
    public string Relationship => RelationshipCode switch
    {
        "18" => "Self",
        "01" => "Spouse",
        "19" => "Child",
        "20" => "Employee",
        "21" => "Unknown",
        "39" => "Organ Donor",
        "40" => "Cadaver Donor",
        "53" => "Life Partner",
        _ => $"Other ({RelationshipCode})"
    };

    public static InsSegment Parse(string[] elements)
    {
        return new InsSegment
        {
            RawElements = elements,
            YesNoResponseCode = elements.ElementAtOrDefault(1) ?? "",
            RelationshipCode = elements.ElementAtOrDefault(2) ?? "",
            MaintenanceTypeCode = elements.ElementAtOrDefault(3) ?? "",
            MaintenanceReasonCode = elements.ElementAtOrDefault(4) ?? "",
            BenefitStatusCode = elements.ElementAtOrDefault(5) ?? "",
            MedicarePlanCode = elements.ElementAtOrDefault(6) ?? "",
            ConsolidatedOmnibusBudgetReconciliationAct = elements.ElementAtOrDefault(7) ?? "",
            EmploymentStatusCode = elements.ElementAtOrDefault(8) ?? "",
            StudentStatusCode = elements.ElementAtOrDefault(9) ?? "",
            HandicapIndicator = elements.ElementAtOrDefault(10) ?? "",
            DateTimePeriodFormatQualifier = elements.ElementAtOrDefault(11) ?? "",
            DateTimePeriod = elements.ElementAtOrDefault(12) ?? "",
        };
    }
}

/// <summary>
/// HD - Health Coverage. Defines a specific benefit plan for the member.
/// </summary>
[EdiSegment("HD")]
public class HdSegment : EdiSegmentBase
{
    public string MaintenanceTypeCode { get; set; } = string.Empty;        // HD01 (001=Change, 021=Add, 024=Cancel, 025=Reinstate)
    public string MaintenanceReasonCode { get; set; } = string.Empty;      // HD02
    public string InsuranceLineCode { get; set; } = string.Empty;          // HD03 (HLT=Health, DEN=Dental, VIS=Vision, etc.)
    public string PlanCoverageDescription { get; set; } = string.Empty;    // HD04 (Plan name or code)
    public string CoverageLevelCode { get; set; } = string.Empty;          // HD05 (EMP=Employee Only, ESP=Emp+Spouse, ECH=Emp+Children, FAM=Family)

    /// <summary>Readable insurance line description.</summary>
    public string InsuranceType => InsuranceLineCode switch
    {
        "HLT" => "Health",
        "DEN" => "Dental",
        "VIS" => "Vision",
        "HMO" => "HMO",
        "PPO" => "PPO",
        "POS" => "POS",
        "EPO" => "EPO",
        "PDG" => "Prescription Drug",
        "MHT" => "Mental Health",
        "LIF" => "Life",
        "LTD" => "Long-Term Disability",
        "STD" => "Short-Term Disability",
        _ => InsuranceLineCode
    };

    /// <summary>Readable coverage level description.</summary>
    public string CoverageLevel => CoverageLevelCode switch
    {
        "EMP" => "Employee Only",
        "ESP" => "Employee + Spouse",
        "ECH" => "Employee + Children",
        "FAM" => "Family",
        "IND" => "Individual",
        "SPC" => "Spouse + Children",
        "SPO" => "Spouse Only",
        "CHD" => "Children Only",
        "DEP" => "Dependents Only",
        "TWO" => "Employee + One",
        _ => CoverageLevelCode
    };

    public static HdSegment Parse(string[] elements)
    {
        return new HdSegment
        {
            RawElements = elements,
            MaintenanceTypeCode = elements.ElementAtOrDefault(1) ?? "",
            MaintenanceReasonCode = elements.ElementAtOrDefault(2) ?? "",
            InsuranceLineCode = elements.ElementAtOrDefault(3) ?? "",
            PlanCoverageDescription = elements.ElementAtOrDefault(4) ?? "",
            CoverageLevelCode = elements.ElementAtOrDefault(5) ?? "",
        };
    }
}

/// <summary>
/// IDC - Identification Card. Information about member ID cards.
/// </summary>
[EdiSegment("IDC")]
public class IdcSegment : EdiSegmentBase
{
    public string PlanCoverageDescription { get; set; } = string.Empty;    // IDC01
    public string IdentificationCardTypeCode { get; set; } = string.Empty; // IDC02 (D=Dental, H=Health, P=Prescription)
    public string IdentificationCardCount { get; set; } = string.Empty;    // IDC03
    public string ActionCode { get; set; } = string.Empty;                 // IDC04 (1=Add, 2=Change, RX=Replace)

    public static IdcSegment Parse(string[] elements)
    {
        return new IdcSegment
        {
            RawElements = elements,
            PlanCoverageDescription = elements.ElementAtOrDefault(1) ?? "",
            IdentificationCardTypeCode = elements.ElementAtOrDefault(2) ?? "",
            IdentificationCardCount = elements.ElementAtOrDefault(3) ?? "",
            ActionCode = elements.ElementAtOrDefault(4) ?? "",
        };
    }
}

/// <summary>
/// EC - Employment Class. Contains employment class codes.
/// </summary>
[EdiSegment("EC")]
public class EcSegment : EdiSegmentBase
{
    public string EmploymentClassCode1 { get; set; } = string.Empty;  // EC01
    public string EmploymentClassCode2 { get; set; } = string.Empty;  // EC02
    public string EmploymentClassCode3 { get; set; } = string.Empty;  // EC03

    public static EcSegment Parse(string[] elements)
    {
        return new EcSegment
        {
            RawElements = elements,
            EmploymentClassCode1 = elements.ElementAtOrDefault(1) ?? "",
            EmploymentClassCode2 = elements.ElementAtOrDefault(2) ?? "",
            EmploymentClassCode3 = elements.ElementAtOrDefault(3) ?? "",
        };
    }
}

/// <summary>
/// ICM - Individual Income. Member salary/income information.
/// </summary>
[EdiSegment("ICM")]
public class IcmSegment : EdiSegmentBase
{
    public string FrequencyCode { get; set; } = string.Empty;      // ICM01 (1=Weekly, 2=Biweekly, 4=Monthly, 6=Annual)
    public decimal WageAmount { get; set; }                         // ICM02
    public string WorkHoursCount { get; set; } = string.Empty;     // ICM03
    public string LocationIdentificationCode { get; set; } = string.Empty; // ICM04

    public static IcmSegment Parse(string[] elements)
    {
        var seg = new IcmSegment
        {
            RawElements = elements,
            FrequencyCode = elements.ElementAtOrDefault(1) ?? "",
            WorkHoursCount = elements.ElementAtOrDefault(3) ?? "",
            LocationIdentificationCode = elements.ElementAtOrDefault(4) ?? "",
        };

        if (decimal.TryParse(elements.ElementAtOrDefault(2), out var wage))
            seg.WageAmount = wage;

        return seg;
    }
}
