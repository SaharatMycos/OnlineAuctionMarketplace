using Marketplace.Modules.Catalog.Contracts;
using Marketplace.SharedKernel;

namespace Marketplace.Modules.Orders.Domain;

internal enum OrderStatus
{
    AgreementPending,
    Agreed,
    DealPaid,
    Received,
    Cancelled,
}

internal enum Handover
{
    Pickup,
    Shipping,
}

internal enum PaymentMethod
{
    Cash,
    BankTransfer,
    MobileQr,
    Other,
}

internal enum PartyRole
{
    Buyer,
    Seller,
}

/// <summary>
/// The deal agreement (feature design §6): the locked price plus handover and payment terms.
/// No money moves through the platform; both sides record their acceptance and confirmations.
/// </summary>
internal sealed class DealAgreement
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid OrderId { get; init; }
    /// <summary>AG-1001, AG-1002, … from a database sequence.</summary>
    public required string Number { get; init; }
    public int Version { get; set; } = 1;
    /// <summary>Locked to the auction result. Never changes (P1).</summary>
    public decimal AgreedPrice { get; init; }
    public Handover Handover { get; set; }
    public DateOnly HandoverDate { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string Notes { get; set; } = "";
    /// <summary>The private item location; its address is revealed only after both accept.</summary>
    public Guid PickupLocationId { get; init; }
    public required string AreaName { get; init; }
    public DateTimeOffset? BuyerAcceptedAt { get; set; }
    public DateTimeOffset? SellerAcceptedAt { get; set; }
    public DateTimeOffset? BuyerPaidAt { get; set; }
    public DateTimeOffset? SellerReceivedAt { get; set; }
    public DateTimeOffset? ReceivedAt { get; set; }
}

/// <summary>Append-only history of the agreement: who did what, on which version.</summary>
internal sealed class DealEvent
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid OrderId { get; init; }
    public int Version { get; init; }
    public Guid? ActorId { get; init; }
    public required string Action { get; init; }
    public string? Detail { get; init; }
    public DateTimeOffset At { get; init; }
}

/// <summary>
/// An order and its deal agreement. Flow (system design §5): agreement_pending → agreed (both accept)
/// → deal_paid (buyer "paid" + seller "received") → received (buyer). Cancelled only through the
/// non-completion flow (feature design §5.2).
/// </summary>
internal sealed class Order
{
    public static readonly TimeSpan AcceptanceWindow = TimeSpan.FromHours(48);
    public static readonly TimeSpan PaymentGrace = TimeSpan.FromDays(3);

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid AuctionId { get; init; }
    public Guid ListingId { get; init; }
    public required string Title { get; init; }
    public Guid BuyerId { get; init; }
    public required string BuyerHandle { get; init; }
    public Guid SellerId { get; init; }
    public required string SellerHandle { get; init; }
    public required string Currency { get; init; }
    /// <summary>What the listing allowed; limits the handover choice.</summary>
    public DeliveryOption Delivery { get; init; }
    public decimal? ShippingCost { get; init; }
    public OrderStatus Status { get; private set; } = OrderStatus.AgreementPending;
    public DateTimeOffset AcceptBy { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }

    public required DealAgreement Agreement { get; init; }
    public List<DealEvent> Events { get; private init; } = [];

    public decimal Total => Agreement.AgreedPrice + (Agreement.Handover == Handover.Shipping ? ShippingCost ?? 0 : 0);

    public static Order Create(
        Guid auctionId, ListingSummary listing, Guid buyerId, string buyerHandle, string sellerHandle,
        decimal price, string currency, string agreementNumber, DateTimeOffset now)
    {
        var handover = listing.Delivery == DeliveryOption.Ship ? Handover.Shipping : Handover.Pickup;
        var order = new Order
        {
            AuctionId = auctionId,
            ListingId = listing.ListingId,
            Title = listing.Title,
            BuyerId = buyerId,
            BuyerHandle = buyerHandle,
            SellerId = listing.SellerId,
            SellerHandle = sellerHandle,
            Currency = currency,
            Delivery = listing.Delivery,
            ShippingCost = listing.ShippingCost,
            AcceptBy = now + AcceptanceWindow,
            CreatedAt = now,
            Agreement = new DealAgreement
            {
                Number = agreementNumber,
                AgreedPrice = price,
                Handover = handover,
                HandoverDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(3)),
                // Cash at handover is the default for pickup (feature design §9.5).
                PaymentMethod = handover == Handover.Pickup ? PaymentMethod.Cash : PaymentMethod.BankTransfer,
                PickupLocationId = listing.LocationId,
                AreaName = listing.AreaName,
            },
        };
        order.Record(null, "created", null, now);
        return order;
    }

    public PartyRole? RoleOf(Guid userId) =>
        userId == BuyerId ? PartyRole.Buyer : userId == SellerId ? PartyRole.Seller : null;

    public PartyRole RequireParty(Guid userId) =>
        RoleOf(userId) ?? throw ProblemException.Forbidden("not_a_party", "Only the buyer and the seller can see or change this deal.");

    /// <summary>Both parties accepted (the exact pickup address may be shown).</summary>
    public bool IsAgreed => Status is OrderStatus.Agreed or OrderStatus.DealPaid or OrderStatus.Received;

    /// <summary>Changes handover/payment terms. The price can't change. Bumps the version and resets acceptances.</summary>
    public bool UpdateTerms(Guid actorId, int version, Handover? handover, DateOnly? handoverDate, PaymentMethod? paymentMethod, string? notes, DateTimeOffset now)
    {
        RequireParty(actorId);
        if (Status != OrderStatus.AgreementPending)
            throw ProblemException.Conflict("agreement_locked", "The agreement is locked once both parties accept it.");
        RequireVersion(version);

        var a = Agreement;
        var changes = new List<string>();
        if (handover is { } h && h != a.Handover)
        {
            if (h == Handover.Pickup && Delivery == DeliveryOption.Ship)
                throw ProblemException.BadRequest("pickup_not_offered", "This item is shipping only.");
            if (h == Handover.Shipping && Delivery == DeliveryOption.Pickup)
                throw ProblemException.BadRequest("shipping_not_offered", "This item is pickup only.");
            a.Handover = h;
            changes.Add($"handover={h}");
        }
        if (handoverDate is { } d && d != a.HandoverDate)
        {
            if (d < DateOnly.FromDateTime(now.UtcDateTime))
                throw ProblemException.BadRequest("invalid_handover_date", "The handover date can't be in the past.");
            a.HandoverDate = d;
            changes.Add($"handover_date={d:yyyy-MM-dd}");
        }
        if (paymentMethod is { } p && p != a.PaymentMethod)
        {
            a.PaymentMethod = p;
            changes.Add($"payment_method={p}");
        }
        if (notes is not null && notes.Trim() != a.Notes)
        {
            if (notes.Length > 1000)
                throw ProblemException.BadRequest("invalid_notes", "Notes can be at most 1,000 characters.");
            a.Notes = notes.Trim();
            changes.Add("notes");
        }
        if (changes.Count == 0)
            return false;

        // A terms change means nobody has accepted *these* terms yet (system design §10).
        a.Version++;
        (a.BuyerAcceptedAt, a.SellerAcceptedAt) = (null, null);
        Record(actorId, "terms_changed", string.Join(", ", changes), now);
        return true;
    }

    public void Accept(Guid actorId, int version, DateTimeOffset now)
    {
        var role = RequireParty(actorId);
        if (Status != OrderStatus.AgreementPending)
            throw ProblemException.Conflict("not_pending", "This agreement isn't waiting for acceptance.");
        RequireVersion(version);

        if (role == PartyRole.Buyer)
            Agreement.BuyerAcceptedAt ??= now;
        else
            Agreement.SellerAcceptedAt ??= now;
        Record(actorId, "accepted", null, now);

        if (Agreement is { BuyerAcceptedAt: not null, SellerAcceptedAt: not null })
            Status = OrderStatus.Agreed;
    }

    /// <summary>Buyer: "I paid". Seller: "I received payment". Deal paid only when both confirm.</summary>
    public void ConfirmPaid(Guid actorId, DateTimeOffset now)
    {
        var role = RequireParty(actorId);
        if (Status is not (OrderStatus.Agreed or OrderStatus.DealPaid))
            throw ProblemException.Conflict("not_agreed", "Both parties must accept the agreement before confirming payment.");

        if (role == PartyRole.Buyer && Agreement.BuyerPaidAt is null)
        {
            Agreement.BuyerPaidAt = now;
            Record(actorId, "confirmed_paid", null, now);
        }
        else if (role == PartyRole.Seller && Agreement.SellerReceivedAt is null)
        {
            Agreement.SellerReceivedAt = now;
            Record(actorId, "confirmed_received", null, now);
        }

        if (Agreement is { BuyerPaidAt: not null, SellerReceivedAt: not null })
            Status = OrderStatus.DealPaid;
    }

    public void MarkReceived(Guid actorId, DateTimeOffset now)
    {
        if (RequireParty(actorId) != PartyRole.Buyer)
            throw ProblemException.Forbidden("buyer_only", "Only the buyer marks the item as received.");
        if (Status != OrderStatus.DealPaid)
            throw ProblemException.Conflict("not_paid", "Both parties must confirm the deal is paid first.");
        Status = OrderStatus.Received;
        Agreement.ReceivedAt = CompletedAt = now;
        Record(actorId, "marked_received", null, now);
    }

    /// <summary>Non-completion (feature design §5.2): the seller may cancel after the acceptance window, or after handover date + grace without payment.</summary>
    public bool CanCancel(DateTimeOffset now) => Status switch
    {
        OrderStatus.AgreementPending => now > AcceptBy,
        OrderStatus.Agreed => now > Agreement.HandoverDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc) + PaymentGrace,
        _ => false,
    };

    public void Cancel(Guid actorId, DateTimeOffset now)
    {
        if (RequireParty(actorId) != PartyRole.Seller)
            throw ProblemException.Forbidden("seller_only", "Only the seller can cancel a deal that wasn't completed.");
        if (!CanCancel(now))
            throw ProblemException.Conflict("cannot_cancel_yet", "You can cancel only after the acceptance window, or after the handover date plus 3 days without payment.");
        Status = OrderStatus.Cancelled;
        CancelledAt = now;
        Record(actorId, "cancelled", null, now);
    }

    private void RequireVersion(int version)
    {
        if (version != Agreement.Version)
            throw ProblemException.Conflict("stale_version", $"The terms changed (now version {Agreement.Version}). Review them and try again.");
    }

    private void Record(Guid? actorId, string action, string? detail, DateTimeOffset now) =>
        Events.Add(new DealEvent { OrderId = Id, Version = Agreement.Version, ActorId = actorId, Action = action, Detail = detail, At = now });
}
