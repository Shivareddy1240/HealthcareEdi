# Healthcare EDI Parser - NuGet Package Suite

A strongly-typed C# library for parsing all major HIPAA X12 5010 healthcare EDI transactions. Each transaction type is a separate NuGet package — install only what you need.

## All 9 Transaction Types — Complete

| Transaction | Description | Parser Class | Package |
|-------------|-------------|-------------|---------|
| **837 P/I/D** | Health Care Claim | `Claim837Parser` | `HealthcareEdi.Transactions.837` |
| **835** | Claim Payment / ERA (Remittance) | `Remittance835Parser` | `HealthcareEdi.Transactions.835` |
| **834** | Benefit Enrollment & Maintenance | `Enrollment834Parser` | `HealthcareEdi.Transactions.834` |
| **270** | Eligibility Inquiry | `Eligibility270271Parser` | `HealthcareEdi.Transactions.270271` |
| **271** | Eligibility Response | `Eligibility270271Parser` | `HealthcareEdi.Transactions.270271` |
| **276** | Claim Status Inquiry | `ClaimStatus276277Parser` | `HealthcareEdi.Transactions.276277` |
| **277** | Claim Status Response | `ClaimStatus276277Parser` | `HealthcareEdi.Transactions.276277` |
| **278** | Prior Authorization | `PriorAuth278Parser` | `HealthcareEdi.Transactions.278` |
| **820** | Premium Payment | `Premium820Parser` | `HealthcareEdi.Transactions.820` |
| **999/997** | Implementation Acknowledgment | `AcknowledgmentParser` | `HealthcareEdi.Transactions.Acknowledgments` |
| **TA1** | Interchange Acknowledgment | `AcknowledgmentParser` | `HealthcareEdi.Transactions.Acknowledgments` |

**140+ xUnit tests** across all transactions. File extension agnostic — works with `.x12`, `.edi`, `.834`, `.835`, `.837`, `.txt`, `.dat`, or any extension.

## Quick Start

### Prerequisites

- **.NET 9 SDK** — [Download](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Visual Studio 2022 v17.12+** (with .NET 9 support)

### Open in Visual Studio

1. Clone/download this repository
2. Open `HealthcareEdi.sln` in Visual Studio
3. Build → Build Solution (Ctrl+Shift+B)
4. Test → Run All Tests (140+ tests)
5. Run the demo: Set `HealthcareEdi.ConsoleDemo` as startup project → F5

### Run Tests

```bash
dotnet test
```

## Usage Examples

### 837 — Parse Claims

```csharp
var parser = new Claim837Parser();
var result = parser.ParseFile(File.ReadAllText("claims.837"));

foreach (var txn in result.Transactions)
{
    Console.WriteLine($"Variant: {txn.Variant}");        // Professional, Institutional, or Dental
    Console.WriteLine($"Provider NPI: {txn.BillingProviderNpi}");
    Console.WriteLine($"Patient: {txn.PatientName}");

    foreach (var claim in txn.Claims)
    {
        Console.WriteLine($"  Claim {claim.PatientAccountNumber}: ${claim.TotalCharge}");
        Console.WriteLine($"  Principal Dx: {claim.PrincipalDiagnosis?.Code}");

        foreach (var line in claim.ServiceLines)
            Console.WriteLine($"    Line {line.LineNumber}: {line.ProcedureCode} ${line.ChargeAmount}");
    }
}
```

### 835 — Process Remittance / ERA

```csharp
var parser = new Remittance835Parser();
var model = parser.ParseFile(File.ReadAllText("era.835")).Transactions[0];

Console.WriteLine($"Payment: ${model.TotalPaymentAmount} via {model.PaymentMethod}");
Console.WriteLine($"Trace: {model.CheckOrTraceNumber}");
Console.WriteLine($"Paid Claims: {model.PaidClaims.Count()}, Denied: {model.DeniedClaims.Count()}");

foreach (var claim in model.Claims)
{
    Console.WriteLine($"  {claim.PatientControlNumber}: Charged ${claim.ChargeAmount}, Paid ${claim.PaymentAmount}");
    foreach (var adj in claim.AllAdjustments)
        Console.WriteLine($"    {adj.GroupCode}-{adj.ReasonCode}: ${adj.Amount}");
}
```

### 834 — Enrollment Processing

```csharp
var parser = new Enrollment834Parser();
var model = parser.ParseFile(File.ReadAllText("enrollment.edi")).Transactions[0];

Console.WriteLine($"Sponsor: {model.Sponsor.SponsorName}");
Console.WriteLine($"Additions: {model.Additions.Count()}, Terminations: {model.Terminations.Count()}");

foreach (var member in model.Members)
{
    Console.WriteLine($"  {member.FullName} - {member.MaintenanceAction} ({member.Relationship})");
    foreach (var cov in member.HealthCoverages)
        Console.WriteLine($"    {cov.InsuranceType}: {cov.PlanName} [{cov.CoverageLevel}] {cov.EffectiveDate}");
}
```

### 271 — Check Eligibility

```csharp
var parser = new Eligibility270271Parser();
var model = parser.Parse271File(File.ReadAllText("response.271")).Transactions[0];

Console.WriteLine($"Active: {model.HasActiveCoverage}");

foreach (var copay in model.Copays)
    Console.WriteLine($"  {copay.ServiceTypeDescription}: ${copay.Amount}");

foreach (var ded in model.Deductibles)
    Console.WriteLine($"  Deductible ({ded.CoverageLevel}): ${ded.Amount} / {ded.TimePeriod}");
```

### 277 — Claim Status

```csharp
var parser = new ClaimStatus276277Parser();
var model = parser.Parse277File(File.ReadAllText("status.277")).Transactions[0];

Console.WriteLine($"Paid: {model.PaidClaims.Count()}, Pending: {model.PendingClaims.Count()}, Denied: {model.DeniedClaims.Count()}");

foreach (var claim in model.AllClaimStatuses)
    Console.WriteLine($"  {claim.TraceNumber}: {claim.StatusCategory}");
```

### 278 — Prior Authorization

```csharp
var parser = new PriorAuth278Parser();
var model = parser.ParseResponseFile(File.ReadAllText("auth.278")).Transactions[0];

foreach (var review in model.ServiceReviews)
    Console.WriteLine($"  {review.Decision} - Auth#: {review.AuthorizationNumber}");
```

### 820 — Premium Payment

```csharp
var parser = new Premium820Parser();
var model = parser.ParseFile(File.ReadAllText("premium.820")).Transactions[0];

Console.WriteLine($"Total Premium: ${model.TotalPremiumAmount}");
foreach (var detail in model.AllPremiumDetails)
    Console.WriteLine($"  {detail.Remittance?.ReferenceId}: ${detail.PremiumAmount}");
```

### 999 — Acknowledgment

```csharp
var parser = new AcknowledgmentParser();
var model = parser.Parse999File(File.ReadAllText("ack.999")).Transactions[0];

Console.WriteLine($"Group {model.AcknowledgedGroupControlNumber}: {model.AcceptedTransactions}/{model.TotalTransactions} accepted");
foreach (var rej in model.RejectedTransactions)
    Console.WriteLine($"  Rejected: {rej.ControlNumber} - {rej.Status}");
```

### PHI Redaction (Any Transaction)

```csharp
model.RedactPhi();
// Names: "DOE, JOHN" → "D**, J***"
// SSN:   "123456789" → "*********"
// DOB:   "19850315"  → "********"
// Bank:  "987654321" → "*********"
```

## Streaming & Batch Processing (GB-Scale Files)

For large files (100k+ transactions, GB-scale), every parser supports three processing modes:

### Mode 1: In-Memory (default — files up to ~100MB)

```csharp
var result = parser.ParseFile(File.ReadAllText("small.835"));
```

### Mode 2: Streaming — One Transaction at a Time

Memory usage proportional to the largest single transaction, not the file size.

```csharp
await foreach (var model in parser.ParseFileStreamingAsync("huge_era.835"))
{
    await SaveToDatabase(model);
    // Previous model's memory is released on next iteration
}
```

### Mode 3: Batch Processing — N Transactions at a Time

Process a configurable batch, do your SQL bulk insert, then get the next batch.

```csharp
await foreach (var batch in parser.ParseFileInBatchesAsync("huge_era.835", batchSize: 500))
{
    Console.WriteLine($"Batch {batch.BatchNumber}: {batch.Transactions.Count} transactions");

    await BulkInsertToSql(batch.Transactions);

    Console.WriteLine($"Progress: {batch.TotalProcessed} total processed, more: {batch.HasMore}");
    // Batch memory released when next batch starts
}
```

### Streaming from a Stream (e.g., network, Azure Blob, S3)

```csharp
using var stream = await blobClient.OpenReadAsync();
await foreach (var model in parser.ParseStreamingAsync(stream))
{
    await ProcessModel(model);
}
```

### Streaming Methods per Parser

| Parser | Streaming | Batch |
|--------|-----------|-------|
| `Claim837Parser` | `ParseFileStreamingAsync`, `ParseStreamingAsync` | `ParseFileInBatchesAsync`, `ParseStreamInBatchesAsync` |
| `Remittance835Parser` | `ParseFileStreamingAsync`, `ParseStreamingAsync` | `ParseFileInBatchesAsync`, `ParseStreamInBatchesAsync` |
| `Enrollment834Parser` | `ParseFileStreamingAsync`, `ParseStreamingAsync` | `ParseFileInBatchesAsync`, `ParseStreamInBatchesAsync` |
| `Eligibility270271Parser` | `Parse270FileStreamingAsync`, `Parse271FileStreamingAsync` | `Parse270FileInBatchesAsync`, `Parse271FileInBatchesAsync` |
| `ClaimStatus276277Parser` | `Parse276FileStreamingAsync`, `Parse277FileStreamingAsync` | `Parse276FileInBatchesAsync`, `Parse277FileInBatchesAsync` |
| `PriorAuth278Parser` | `ParseRequestFileStreamingAsync`, `ParseResponseFileStreamingAsync` | `ParseRequestFileInBatchesAsync`, `ParseResponseFileInBatchesAsync` |
| `Premium820Parser` | `ParseFileStreamingAsync`, `ParseStreamingAsync` | `ParseFileInBatchesAsync`, `ParseStreamInBatchesAsync` |
| `AcknowledgmentParser` | `Parse999FileStreamingAsync`, `Parse999StreamingAsync` | `Parse999FileInBatchesAsync`, `Parse999StreamInBatchesAsync` |

## Solution Structure

```
HealthcareEdi.sln (22 projects)
├── src/
│   ├── HealthcareEdi.Core/                          # Core engine
│   │   ├── Attributes/                              # [EdiSegment], [EdiLoop]
│   │   ├── Envelopes/                               # ISA, GS, ST
│   │   ├── Models/Base/                             # EdiTransactionBase, EdiBatchResult
│   │   ├── Parsing/                                 # DelimiterContext, EdiTokenizer,
│   │   │                                            # StreamingEdiTokenizer, EdiBatchProcessor,
│   │   │                                            # ParserOptions, Exceptions
│   │   ├── Segments/                                # NM1, REF, N3, N4, DTP, DMG, HL, SBR, PRV, PER
│   │   └── Validation/                              # EdiValidationIssue
│   ├── HealthcareEdi.Transactions.837/              # 837 P/I/D Claims
│   ├── HealthcareEdi.Transactions.835/              # 835 ERA / Remittance
│   ├── HealthcareEdi.Transactions.834/              # 834 Enrollment
│   ├── HealthcareEdi.Transactions.270271/           # 270/271 Eligibility
│   ├── HealthcareEdi.Transactions.276277/           # 276/277 Claim Status
│   ├── HealthcareEdi.Transactions.278/              # 278 Prior Auth
│   ├── HealthcareEdi.Transactions.820/              # 820 Premium Payment
│   └── HealthcareEdi.Transactions.Acknowledgments/  # 999/997/TA1
├── tests/                                           # 9 test projects, 140+ tests
│   ├── HealthcareEdi.Core.Tests/
│   ├── HealthcareEdi.Transactions.837.Tests/
│   ├── HealthcareEdi.Transactions.835.Tests/
│   ├── HealthcareEdi.Transactions.834.Tests/
│   ├── HealthcareEdi.Transactions.270271.Tests/
│   ├── HealthcareEdi.Transactions.276277.Tests/
│   ├── HealthcareEdi.Transactions.278.Tests/
│   ├── HealthcareEdi.Transactions.820.Tests/
│   └── HealthcareEdi.Transactions.Acknowledgments.Tests/
└── samples/
    └── HealthcareEdi.ConsoleDemo/
```

## Architecture

### Three-Stage Parser Pipeline

1. **Delimiter Detection** — Reads ISA as a fixed 106-character block. Extracts element separator (pos 3), repetition separator (pos 82), component separator (pos 104), and segment terminator (pos 105). Completely delimiter-agnostic.
2. **Tokenization** — Splits file by segment terminator, groups into ST/SE transaction sets. Handles BOM, line breaks, missing terminators.
3. **State Machine Parsing** — Walks segments, tracks loop context, routes each segment to the correct model location using segment IDs and qualifier values.

### Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **Validation Modes** | Strict / Lenient / None | Real-world files are rarely 100% compliant |
| **Segment Repetition** | `List<T>` + `Dictionary<qualifier, List<T>>` | Both iteration and keyed-lookup patterns |
| **Composite Elements** | Typed sub-objects (`ProcedureCode`, `DiagnosisCode`, `StatusInfo`) | Type safety for complex composites |
| **837 Variant Detection** | ST03 → GS08 → SV segment scan | Cascading fallback for unreliable data |
| **Error Isolation** | Per-transaction at ST/SE boundary | One bad claim doesn't kill a batch of 500 |
| **PHI Redaction** | `RedactPhi()` on every model | HIPAA Safe Harbor compliance |
| **Streaming** | `IAsyncEnumerable<T>` + `EdiBatchProcessor<T>` | GB-scale files without exceeding memory |
| **File Extensions** | Ignored — parser reads content only | Works with .x12, .edi, .834, .txt, anything |
| **Extensibility** | `IExtendedParser<T>` plugin interface | Payer-specific handling without modifying core |

## Development Sprints — All Complete

| Sprint | Scope | Status |
|--------|-------|--------|
| Sprint 1 | Core engine + 837 P/I/D | ✅ Complete |
| Sprint 2 | 835 Remittance / ERA | ✅ Complete |
| Sprint 3 | 834 Benefit Enrollment | ✅ Complete |
| Sprint 4 | 270/271 Eligibility | ✅ Complete |
| Sprint 5 | 276/277, 278, 820, 999/997/TA1 | ✅ Complete |
| Sprint 6 | Streaming & Batch Processing | ✅ Complete |

## License

MIT
