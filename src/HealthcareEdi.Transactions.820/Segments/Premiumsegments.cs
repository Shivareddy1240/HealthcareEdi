using HealthcareEdi.Core.Attributes;
using HealthcareEdi.Core.Envelopes;
using HealthcareEdi.Core.Models.Base;
using HealthcareEdi.Core.Parsing;
using HealthcareEdi.Core.Segments;
using HealthcareEdi.Core.Validation;
using System.Diagnostics;

namespace HealthcareEdi.Transactions.Premium820.Segments;

/// <summary>BPR - Financial Information (reused pattern from 835).</summary>
[EdiSegment("BPR")]
public class BprSegment820 : EdiSegmentBase
{
    public string TransactionHandlingCode { get; set; } = string.Empty;
    public decimal TotalPremiumAmount { get; set; }
    public string CreditDebitFlag { get; set; } = string.Empty;
    public string PaymentMethodCode { get; set; } = string.Empty;
    public string PaymentDate { get; set; } = string.Empty;

    public bool IsEft => PaymentMethodCode == "ACH";

    public static BprSegment820 Parse(string[] elements)
    {
        var seg = new BprSegment820
        {
            RawElements = elements,
            TransactionHandlingCode = elements.ElementAtOrDefault(1) ?? "",
            CreditDebitFlag = elements.ElementAtOrDefault(3) ?? "",
            PaymentMethodCode = elements.ElementAtOrDefault(4) ?? "",
            PaymentDate = elements.ElementAtOrDefault(15) ?? "",
        };
        if (decimal.TryParse(elements.ElementAtOrDefault(2), out var amt)) seg.TotalPremiumAmount = amt;
        return seg;
    }
}

/// <summary>RMR - Remittance Advice Accounts Receivable Detail.</summary>
[EdiSegment("RMR")]
public class RmrSegment : EdiSegmentBase
{
    public string ReferenceIdQualifier { get; set; } = string.Empty;   // RMR01
    public string ReferenceId { get; set; } = string.Empty;            // RMR02
    public string PaymentActionCode { get; set; } = string.Empty;      // RMR03
    public decimal DetailPremiumAmount { get; set; }                    // RMR04
    public decimal TotalPremiumAmount { get; set; }                     // RMR05
    public decimal BalanceDue { get; set; }                             // RMR06

    public static RmrSegment Parse(string[] elements)
    {
        var seg = new RmrSegment
        {
            RawElements = elements,
            ReferenceIdQualifier = elements.ElementAtOrDefault(1) ?? "",
            ReferenceId = elements.ElementAtOrDefault(2) ?? "",
            PaymentActionCode = elements.ElementAtOrDefault(3) ?? "",
        };
        if (decimal.TryParse(elements.ElementAtOrDefault(4), out var detail)) seg.DetailPremiumAmount = detail;
        if (decimal.TryParse(elements.ElementAtOrDefault(5), out var total)) seg.TotalPremiumAmount = total;
        if (decimal.TryParse(elements.ElementAtOrDefault(6), out var bal)) seg.BalanceDue = bal;
        return seg;
    }
}

/// <summary>ENT - Entity for premium payment detail.</summary>
[EdiSegment("ENT")]
public class EntSegment : EdiSegmentBase
{
    public string AssignedNumber { get; set; } = string.Empty;         // ENT01
    public string EntityIdentifierCode { get; set; } = string.Empty;   // ENT02
    public string IdentificationQualifier { get; set; } = string.Empty; // ENT03
    public string IdentificationCode { get; set; } = string.Empty;     // ENT04

    public static EntSegment Parse(string[] elements)
    {
        return new EntSegment
        {
            RawElements = elements,
            AssignedNumber = elements.ElementAtOrDefault(1) ?? "",
            EntityIdentifierCode = elements.ElementAtOrDefault(2) ?? "",
            IdentificationQualifier = elements.ElementAtOrDefault(3) ?? "",
            IdentificationCode = elements.ElementAtOrDefault(4) ?? "",
        };
    }
}
