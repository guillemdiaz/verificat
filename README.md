# VeriFicat

VeriFicat is a Proof of Concept .NET console application that simulates the core ideas 
behind the Spanish Tax Agency's (AEAT) Verifactu regulation.

The project focuses on:
- generating chained invoices using SHA-256 hashes
- detecting invoice tampering
- maintaining transaction integrity in SQL Server
- exporting AEAT-style XML records

## Tech Stack

| Component | Technology |
|---|---|
| Framework | .NET 10 (C# 14) Console Application |
| Database | SQL Server |
| Data Access | ADO.NET (`Microsoft.Data.SqlClient`) |
| Cryptography | `System.Security.Cryptography` (SHA-256) |
| XML Processing | `System.Xml.Linq` |

## CLI Features

| Option | Command | Description |
|---|---|---|
| 1 | Generate | Generates test invoices and appends them to the cryptographic chain |
| 2 | View | Displays stored invoices and their hashes |
| 3 | Export | Generates AEAT-style XML output |
| 4 | Verify | Audits the invoice chain integrity |
| 5 | Tamper | Simulates invoice manipulation |
| 6 | Reset | Clears the database for testing |

## System Demonstration

### 1. Invoice Chain Generation

Each invoice stores its own SHA-256 hash together with the hash of the previous invoice.

```text
>_ 1

[OK] Factura VF26-001 creada.
    > HASH: b8daac67532b7965a308019a85a5413f6e2b270dba1e5b5f690105543a4181c7

[OK] Factura VF26-002 creada.
    > HASH: c75e8fd190bbfeaa73581e45a11975447412990c77cb140f91df9239b925eeab
```

### 2. Tampering Detection

If a database record is modified for some reason, the verification process detects that
the stored hash no longer matches the recalculated value.

```text
>_ 5

ID de la factura a manipular: 1
Tipus de manipulació:
    1. Modificar l'import
    2. Esborrar la factura
Opció: 1
Nou import total (ex: 10.50): 95

[WARN] Factura ID 1 modificada => ImportTotal: 95.00€
> Executa l'opció 4 per verificar la cadena.
```

```text
>_ 4

[FAIL] Cadena compromesa - 1 error(s) detectat(s):
    > ID 1 - manipulació detectada (empremta no coincideix).
```

### 3. AEAT XML Export

The system can export invoices into into valid SOAP Envelopes compliant with AEAT, 
including the chaining hashes (`Huella` and `HuellaAnterior`).

> **NOTE:** Reduced example of the generated XML

```xml
<?xml version="1.0" encoding="UTF-8"?>
<soapenv:Envelope>
  <soapenv:Body>
    <sum:RegistroFactura>
      <sum1:RegistroAlta>

        <sum1:Encadenamiento>
          <sum1:RegistroAnterior>
            <sum1:IDEmisorFactura>41562599A</sum1:IDEmisorFactura>
            <sum1:Huella>
              b8daac67532b7965a308019a85a5413f6e2b270dba1e5b5f690105543a4181c7
            </sum1:Huella>
          </sum1:RegistroAnterior>
        </sum1:Encadenamiento>

        <sum1:TipoHuella>01</sum1:TipoHuella>

        <sum1:Huella>
          c75e8fd190bbfeaa73581e45a11975447412990c77cb140f91df9239b925eeab
        </sum1:Huella>

      </sum1:RegistroAlta>
    </sum:RegistroFactura>
  </soapenv:Body>
</soapenv:Envelope>
```

## Getting Started

### Prerequisites

- .NET 10 SDK
- SQL Server

### Setup

#### 1. Clone the repository

```bash
git clone https://github.com/guillemdiaz/verificat.git
cd verificat
```

#### 2. Configure the database connection

Update `appsettings.json` with your SQL Server connection string.

Example:

```json
{
  "ConnectionStrings": {
    "VerificatDB": "Server=.;Database=VerificatDB;Integrated Security=True;TrustServerCertificate=True;"
  }
}
```

#### 3. Initialize the database

Run the SQL scripts located in the `Database/` folder to create:
- the `RegistresFacturacio` table
- the uniqueness constraint

#### 4. Run the application

```bash
dotnet run
```

## Future Improvements

- Store a global accumulated hash to detect silent invoice deletions
