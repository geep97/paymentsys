# Demo recording script

Goal: a 2-3 minute video that shows the payment lifecycle end to end, not just a happy-path checkout. Record at 1080p, keep the terminal and browser zoomed in, and narrate what is being proven.

## Before recording

1. Use Paystack **test** keys in user secrets:
   - `Paystack:SecretKey` = test secret key
   - `Paystack:CallbackUrl` = your public callback URL
2. Set a demo merchant key: `Merchant:ApiKey`.
3. Start the API:
   ```powershell
   dotnet run --project PaymentSys
   ```
4. Open the API docs at `/scalar/v1` and `/health`.
5. Expose the API publicly so Paystack can deliver webhooks. Either deploy it or use a tunnel:
   ```powershell
   cloudflared tunnel --url http://localhost:5000
   ```
   Set the Paystack dashboard webhook URL to `https://<tunnel-or-host>/api/webhooks/paystack`.
6. Open two windows: the API docs/Postman on one side, SQL Server results (or the `WebhookLogs` endpoint output) on the other.
7. Clear or use unique values for `initiateKey` in Postman so the demo does not replay an old response.

## Shot list

### 0:00 - Hook

Say: "Anyone can redirect to Paystack. The interesting part is what happens after the customer pays, when the webhook is duplicated, or when you need to refund and keep the books correct."

Show the repo README with the architecture diagram.

### 0:15 - Health and docs

- Hit `GET /health` and show `status: healthy`, `database: reachable`.
- Open `/scalar/v1` and expand `POST /api/payments`.

### 0:30 - Initiate a payment

- Send `POST /api/payments` with `X-Api-Key` and a unique `Idempotency-Key`:
  ```json
  { "email": "buyer@example.com", "amountPesewas": 1500 }
  ```
- Show `201 Created`, the generated reference, and the Paystack authorization URL.
- Send the same request again with the same key and show the replayed response and that no second Paystack initialization happened (the reference is identical).

### 0:55 - Pay with a test card

Open the authorization URL and pay with Paystack's test card:

- Card: `4084 0840 8408 4081`
- CVV: `408`
- Expiry: any future date
- PIN: `0000`
- OTP: `123456`

### 1:15 - Webhook and verification

If using a tunnel, show the webhook arriving. If not, send the signed `charge.success` request from the Postman collection.

Then call `GET /api/payments/{reference}` and show `status: Success` and `paidAt`.

### 1:35 - Show the ledger

Query the journal entries for the payment:

```sql
SELECT je.Reference, a.AccountNumber, a.Name, l.DebitPesewas, l.CreditPesewas
FROM JournalEntries je
JOIN JournalEntryLines l ON l.JournalEntryId = je.Id
JOIN Accounts a ON a.Id = l.AccountId
WHERE je.PaymentTransactionId = (SELECT Id FROM PaymentTransactions WHERE Reference = '<reference>');
```

Show the three lines: settlement net, fee expense, revenue gross. Say: "Debits equal credits, and the database rejects anything else."

### 1:55 - Refund and reversal

- Send `POST /api/payments/{reference}/refund` with a new `Idempotency-Key` and `{ "merchantNote": "Demo refund" }`.
- Show `200 OK` and `status: Reversed`.
- Re-run the ledger query and show the `REV-<reference>` entry with debits and credits flipped.
- Send the refund request again with the same key and show it replays without a second Paystack refund.

### 2:20 - Close

Say: "Retries are safe, duplicate webhooks are ignored, amounts are verified, refunds reverse the books, and the database itself enforces the ledger. The repo has 43 tests and CI running on every push."

Show the GitHub Actions badge and the README.

## Talking points to keep handy

- Idempotency: same key + same payload replays; different payload gets 422; in-progress gets 409.
- Webhook security: HMAC-SHA512 signature, fixed-time comparison, event-ID deduplication.
- Ledger: double-entry, fees split correctly, posted entries immutable, reversals instead of edits.
- State machine: `Pending -> Success/Failed/Reversed`, `Success -> Reversed` only.
- Verification: amount and currency must match before posting.
- After deploying, switch the demo to Paystack test keys and never expose live secrets.
