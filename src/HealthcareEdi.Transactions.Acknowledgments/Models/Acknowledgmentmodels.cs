using HealthcareEdi.Core.Models.Base;
using HealthcareEdi.Transactions.Acknowledgments.Segments;

namespace HealthcareEdi.Transactions.Acknowledgments.Models;

/// <summary>Individual transaction set acknowledgment within a 999/997.</summary>
public class TransactionSetAcknowledgment
{
    public Ak2Segment Header { get; set; } = new();
    public Ik5Segment? Trailer { get; set; }
    public List<string> ErrorSegments { get; set; } = []; // IK3/IK4 raw segments for error details

    public string TransactionId => Header.TransactionSetIdentifierCode;
    public string ControlNumber => Header.TransactionSetControlNumber;
    public bool IsAccepted => Trailer?.IsAccepted ?? false;
    public bool IsRejected => Trailer?.IsRejected ?? false;
    public string Status => Trailer?.AcknowledgmentDescription ?? "Unknown";
}

/// <summary>999/997 - Implementation/Functional Acknowledgment.</summary>
public class Acknowledgment999Model : EdiTransactionBase
{
    public Ak1Segment GroupResponse { get; set; } = new();
    public Ak9Segment? GroupTrailer { get; set; }
    public List<TransactionSetAcknowledgment> TransactionAcknowledgments { get; set; } = [];

    public string AcknowledgedGroupType => GroupResponse.FunctionalIdentifierCode;
    public string AcknowledgedGroupControlNumber => GroupResponse.GroupControlNumber;
    public bool IsGroupAccepted => GroupTrailer?.IsAccepted ?? false;
    public bool IsGroupRejected => GroupTrailer?.IsRejected ?? false;
    public int TotalTransactions => GroupTrailer?.NumberOfTransactionSetsIncluded ?? 0;
    public int AcceptedTransactions => GroupTrailer?.NumberOfTransactionSetsAccepted ?? 0;

    public IEnumerable<TransactionSetAcknowledgment> RejectedTransactions =>
        TransactionAcknowledgments.Where(t => t.IsRejected);
    public IEnumerable<TransactionSetAcknowledgment> AcceptedTransactionSets =>
        TransactionAcknowledgments.Where(t => t.IsAccepted);
}

/// <summary>TA1 - Interchange Acknowledgment (standalone, not inside ST/SE).</summary>
public class Ta1Model : EdiTransactionBase
{
    public Ta1Segment Acknowledgment { get; set; } = new();
    public bool IsAccepted => Acknowledgment.IsAccepted;
    public bool IsRejected => Acknowledgment.IsRejected;
    public string AcknowledgedControlNumber => Acknowledgment.InterchangeControlNumber;
}
