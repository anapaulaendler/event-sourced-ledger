namespace Ledger.Domain;

public sealed record Posting
{
    public Guid AccountId { get; }
    public Money? Debit { get; }
    public Money? Credit { get; }

    public Posting(Guid accountId, Money? debit, Money? credit)
    {
        if ((debit is null) == (credit is null))
            throw new ArgumentException("Posting must have exactly one of Debit or Credit", nameof(debit));

        AccountId = accountId;
        Debit = debit;
        Credit = credit;
    }

    public static Posting FromDebit(Guid accountId, Money amount) => new(accountId, amount, null);
    public static Posting FromCredit(Guid accountId, Money amount) => new(accountId, null, amount);

    public Money Amount => Debit ?? Credit!;
}
