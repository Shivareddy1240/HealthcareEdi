# Healthcare EDI Parser - NuGet Package

A strongly-typed C# library for parsing HIPAA X12 5010 healthcare EDI transactions.

## Supported Transactions

| Transaction | Description | Status |
|-------------|-------------|--------|
| **837 P/I/D** | Health Care Claim (Professional, Institutional, Dental) | ✅ Implemented |
| **835** | Claim Payment/Advice (ERA/Remittance) | 📋 Stub ready |
| **834** | Benefit Enrollment and Maintenance | 📋 Stub ready |
| **270/271** | Eligibility Inquiry & Response | 📋 Stub ready |
| **276/277** | Claim Status Inquiry & Response | 📋 Stub ready |
| **278** | Prior Authorization | 📋 Stub ready |
| **820** | Premium Payment | 📋 Stub ready |
| **999/997/TA1** | Acknowledgments | 📋 Stub ready |

## Quick Start

### Prerequisites

- **.NET 9 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Visual Studio 2022 v17.12+** (with .NET 9 support)

### Open in Visual Studio

1. Clone/download this repository
2. Open `HealthcareEdi.sln` in Visual Studio
3. Build → Build Solution (Ctrl+Shift+B)
4. Run the demo: Set `HealthcareEdi.ConsoleDemo` as startup project → F5

### Parse an 837 File (Code Example)

```csharp
using HealthcareEdi.Core.Parsing;
using HealthcareEdi.Transactions.Claim837.Parsing;

// Configure parser
var options = new ParserOptions
{
    ValidationMode = ValidationMode.Lenient,
};

var parser = new Claim837Parser(options);

// Parse file
var content = File.ReadAllText("path/to/837P.x12");
var result = parser.ParseFile(content);

// Access strongly-typed data
foreach (var txn in result.Transactions)
{
    Console.WriteLine($"Variant: {txn.Variant}");
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

### Run Tests

```bash
dotnet test
```

## Solution Structure

```
HealthcareEdi.sln
├── src/
│   ├── HealthcareEdi.Core/              # Core engine, base models, envelopes
│   │   ├── Attributes/                  # [EdiSegment], [EdiLoop] attributes
│   │   ├── Envelopes/                   # ISA, GS, ST segment models
│   │   ├── Models/Base/                 # EdiTransactionBase, EdiBatchResult
│   │   ├── Parsing/                     # DelimiterContext, EdiTokenizer, ParserOptions
│   │   ├── Segments/                    # Common segments (NM1, REF, N3, N4, DTP, etc.)
│   │   └── Validation/                  # EdiValidationIssue
│   ├── HealthcareEdi.Transactions.837/  # 837 P/I/D parser (PROOF OF CONCEPT)
│   │   ├── Loops/                       # Loop models (2000A-C, 2300, 2400, COB)
│   │   ├── Models/                      # Claim837PModel, Claim837IModel, Claim837DModel
│   │   ├── Parsing/                     # Claim837Parser
│   │   └── Segments/                    # CLM, HI, SV1, SV2, SV3, CL1
│   ├── HealthcareEdi.Transactions.835/  # 835 (Remittance) - stub
│   ├── HealthcareEdi.Transactions.834/  # 834 (Enrollment) - stub
│   ├── HealthcareEdi.Transactions.270271/ # 270/271 (Eligibility) - stub
│   ├── HealthcareEdi.Transactions.276277/ # 276/277 (Claim Status) - stub
│   ├── HealthcareEdi.Transactions.278/  # 278 (Prior Auth) - stub
│   ├── HealthcareEdi.Transactions.820/  # 820 (Premium Payment) - stub
│   └── HealthcareEdi.Transactions.Acknowledgments/ # 999/997/TA1 - stub
├── tests/
│   ├── HealthcareEdi.Core.Tests/        # Delimiter, tokenizer tests
│   └── HealthcareEdi.Transactions.837.Tests/ # 837 parser tests + sample X12 files
└── samples/
    └── HealthcareEdi.ConsoleDemo/       # Working console demo
```

## Architecture Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **Validation Modes** | Strict / Lenient / None | Real-world files are rarely 100% compliant |
| **Segment Repetition** | List + Dictionary by qualifier | Both iteration and keyed-lookup patterns |
| **Composite Elements** | Typed sub-objects (ProcedureCode, DiagnosisCode) | Type safety for complex composites |
| **837 Variant Detection** | ST03 → GS08 → SV segment scan | Cascading fallback for unreliable data |
| **Error Isolation** | Per-transaction (ST/SE boundary) | One bad claim doesn't kill batch |
| **PHI Redaction** | RedactPhi() on every model | HIPAA Safe Harbor compliance |
| **Streaming** | IAsyncEnumerable (planned) | 100k+ claim files |

## Development Roadmap

### Sprint 1 (Current) - Foundation + 837 PoC ✅
- [x] Core engine (delimiters, tokenizer, envelopes)
- [x] Base models with validation and PHI redaction
- [x] 837 P/I/D parser with COB and Patient loop
- [x] Unit tests with sample files
- [x] Console demo

### Sprint 2 - 835 Remittance
- [ ] BPR, TRN, CLP, CAS, SVC segment models
- [ ] Remittance835Model with claim-level and service-level parsing
- [ ] CAS adjustment group/reason/amount handling
- [ ] PLB provider-level adjustments

### Sprint 3 - 834 Enrollment
- [ ] INS, HD, IDC segment models
- [ ] Enrollment834Model with member-level loops
- [ ] Maintenance type code handling (add/change/term)
- [ ] Loop 2700/2750 reporting categories

### Sprint 4 - 270/271 Eligibility
- [ ] EB segment parsing with service type codes
- [ ] Eligibility270Model / Eligibility271Model pair
- [ ] Benefit coverage level and time period handling

### Sprint 5 - 276/277, 278, 820, Acknowledgments
- [ ] Claim status models
- [ ] Prior auth request/response models
- [ ] Premium payment model with 834 cross-reference
- [ ] 999/997/TA1 acknowledgment parsing

### Sprint 6 - Production Hardening
- [ ] IAsyncEnumerable streaming for large files
- [ ] NuGet packaging with proper metadata
- [ ] Integration tests with top-10 payer companion guide samples
- [ ] Performance benchmarks (target: 10k claims/sec)
- [ ] XML documentation for IntelliSense

## License

MIT
