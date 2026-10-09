using Marketplace.Modules.Catalog.Contracts;
using Marketplace.Modules.Orders.Domain;
using Marketplace.SharedKernel;

namespace Marketplace.UnitTests.Orders;

/// <summary>Deal flow rules (system design §10).</summary>
public class OrderTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Seller = Guid.NewGuid(), Buyer = Guid.NewGuid(), Stranger = Guid.NewGuid();

    private static Order NewOrder(DeliveryOption delivery = DeliveryOption.Both)
    {
        var listing = new ListingSummary(Guid.NewGuid(), Seller, "Trek FX 3", delivery, 15, Guid.NewGuid(), "Wicker Park, Chicago");
        return Order.Create(Guid.NewGuid(), listing, Buyer, "bob", "alice", 320, "USD", "AG-1001", T0);
    }

    [Fact]
    public void Defaults_to_pickup_with_cash_and_a_48h_acceptance_window()
    {
        var order = NewOrder();
        Assert.Equal(Handover.Pickup, order.Agreement.Handover);
        Assert.Equal(PaymentMethod.Cash, order.Agreement.PaymentMethod);
        Assert.Equal(T0.AddHours(48), order.AcceptBy);
        Assert.Equal(320, order.Total);
        Assert.Equal("created", Assert.Single(order.Events).Action);
    }

    [Fact]
    public void Agreed_only_after_both_accept()
    {
        var order = NewOrder();
        order.Accept(Buyer, 1, T0.AddMinutes(1));
        Assert.Equal(OrderStatus.AgreementPending, order.Status);
        Assert.False(order.IsAgreed);

        order.Accept(Seller, 1, T0.AddMinutes(2));
        Assert.Equal(OrderStatus.Agreed, order.Status);
        Assert.True(order.IsAgreed);
    }

    [Fact]
    public void Terms_change_bumps_the_version_and_resets_acceptance()
    {
        var order = NewOrder();
        order.Accept(Seller, 1, T0.AddMinutes(1));

        Assert.True(order.UpdateTerms(Buyer, 1, Handover.Shipping, null, PaymentMethod.BankTransfer, "Please pack well", T0.AddMinutes(2)));
        Assert.Equal(2, order.Agreement.Version);
        Assert.Null(order.Agreement.SellerAcceptedAt);
        Assert.Equal(335, order.Total);                         // shipping added; agreed price unchanged
        Assert.Equal(320, order.Agreement.AgreedPrice);

        // Accepting the old version fails: the seller must see the new terms first.
        Assert.Equal("stale_version", Assert.Throws<ProblemException>(() => order.Accept(Seller, 1, T0.AddMinutes(3))).Code);
    }

    [Fact]
    public void No_op_terms_change_keeps_the_version()
    {
        var order = NewOrder();
        Assert.False(order.UpdateTerms(Buyer, 1, Handover.Pickup, null, PaymentMethod.Cash, null, T0.AddMinutes(1)));
        Assert.Equal(1, order.Agreement.Version);
    }

    [Fact]
    public void Handover_must_be_offered_by_the_listing()
    {
        var pickupOnly = NewOrder(DeliveryOption.Pickup);
        Assert.Equal("shipping_not_offered",
            Assert.Throws<ProblemException>(() => pickupOnly.UpdateTerms(Buyer, 1, Handover.Shipping, null, null, null, T0)).Code);
    }

    [Fact]
    public void Terms_are_locked_after_agreement()
    {
        var order = Agreed();
        Assert.Equal("agreement_locked",
            Assert.Throws<ProblemException>(() => order.UpdateTerms(Buyer, 1, null, null, null, "late change", T0.AddMinutes(5))).Code);
    }

    [Fact]
    public void Deal_paid_needs_both_confirmations_then_buyer_marks_received()
    {
        var order = Agreed();
        Assert.Equal("not_paid", Assert.Throws<ProblemException>(() => order.MarkReceived(Buyer, T0.AddHours(1))).Code);

        order.ConfirmPaid(Buyer, T0.AddHours(1));
        Assert.Equal(OrderStatus.Agreed, order.Status);
        order.ConfirmPaid(Seller, T0.AddHours(2));
        Assert.Equal(OrderStatus.DealPaid, order.Status);

        Assert.Equal("buyer_only", Assert.Throws<ProblemException>(() => order.MarkReceived(Seller, T0.AddHours(3))).Code);
        order.MarkReceived(Buyer, T0.AddHours(3));
        Assert.Equal(OrderStatus.Received, order.Status);
        Assert.Equal(T0.AddHours(3), order.CompletedAt);
    }

    [Fact]
    public void Payment_confirmation_needs_an_agreement_first()
    {
        var order = NewOrder();
        Assert.Equal("not_agreed", Assert.Throws<ProblemException>(() => order.ConfirmPaid(Buyer, T0.AddMinutes(1))).Code);
    }

    [Fact]
    public void Only_the_two_parties_can_act()
    {
        var order = NewOrder();
        Assert.Equal("not_a_party", Assert.Throws<ProblemException>(() => order.Accept(Stranger, 1, T0)).Code);
        Assert.Null(order.RoleOf(Stranger));
    }

    [Fact]
    public void Seller_can_cancel_only_after_the_acceptance_window()
    {
        var order = NewOrder();
        Assert.Equal("cannot_cancel_yet", Assert.Throws<ProblemException>(() => order.Cancel(Seller, T0.AddHours(47))).Code);
        Assert.Equal("seller_only", Assert.Throws<ProblemException>(() => order.Cancel(Buyer, T0.AddHours(49))).Code);

        order.Cancel(Seller, T0.AddHours(49));
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    private static Order Agreed()
    {
        var order = NewOrder();
        order.Accept(Buyer, 1, T0.AddMinutes(1));
        order.Accept(Seller, 1, T0.AddMinutes(2));
        return order;
    }
}
