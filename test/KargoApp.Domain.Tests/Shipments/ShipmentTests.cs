using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace KargoApp.Shipments;

// Saf unit test: ABP modülü ve veritabanı yok, sadece entity'nin kendi kuralları.
public class ShipmentTests
{
    private static readonly DateTime Now = new(2026, 10, 18, 10, 0, 0);

    private static Shipment NewShipment() =>
        new(Guid.NewGuid(), "KRG0000000001", "Ahmet", "Ayşe", "Ankara", 2.5m);

    // Kargoyu istenen duruma gerçek okutma sırasıyla getirir (Status private set olduğu için).
    private static Shipment ShipmentIn(ShipmentStatus status)
    {
        var shipment = NewShipment();
        if (status == ShipmentStatus.Cancelled)
        {
            shipment.Cancel();
            return shipment;
        }

        var path = new[]
        {
            MovementType.DepartedBranch,   // → InTransit
            MovementType.ArrivedAtBranch,  // → AtBranch
            MovementType.OutForDelivery,   // → OutForDelivery
            MovementType.Delivered         // → Delivered
        };

        foreach (var movement in path)
        {
            if (shipment.Status == status)
            {
                break;
            }
            shipment.ApplyMovement(movement, "IST01", Now);
        }

        return shipment;
    }

    // Geçiş tablosunun her satırı ayrı bir test.
    [Theory]
    [InlineData(ShipmentStatus.Created, MovementType.DepartedBranch, ShipmentStatus.InTransit)]
    [InlineData(ShipmentStatus.InTransit, MovementType.ArrivedAtBranch, ShipmentStatus.AtBranch)]
    [InlineData(ShipmentStatus.AtBranch, MovementType.DepartedBranch, ShipmentStatus.InTransit)]
    [InlineData(ShipmentStatus.AtBranch, MovementType.OutForDelivery, ShipmentStatus.OutForDelivery)]
    [InlineData(ShipmentStatus.OutForDelivery, MovementType.Delivered, ShipmentStatus.Delivered)]
    [InlineData(ShipmentStatus.OutForDelivery, MovementType.DeliveryFailed, ShipmentStatus.AtBranch)]
    public void ApplyMovement_GecerliGecis_DurumuDegistirir(
        ShipmentStatus from, MovementType movement, ShipmentStatus expected)
    {
        // Arrange
        var shipment = ShipmentIn(from);
        var occurredAt = Now.AddHours(1);

        // Act
        shipment.ApplyMovement(movement, "ANK01", occurredAt);

        // Assert
        shipment.Status.ShouldBe(expected);
        shipment.LastBranchCode.ShouldBe("ANK01");
        shipment.LastMovementAt.ShouldBe(occurredAt);
    }

    [Theory]
    [InlineData(ShipmentStatus.Created, MovementType.Delivered)]
    [InlineData(ShipmentStatus.InTransit, MovementType.OutForDelivery)]
    [InlineData(ShipmentStatus.AtBranch, MovementType.Delivered)]
    [InlineData(ShipmentStatus.Delivered, MovementType.DepartedBranch)]
    [InlineData(ShipmentStatus.Delivered, MovementType.Delivered)]
    [InlineData(ShipmentStatus.Cancelled, MovementType.DepartedBranch)]
    public void ApplyMovement_GecersizGecis_HataFirlatirVeDurumuDegistirmez(
        ShipmentStatus from, MovementType movement)
    {
        var shipment = ShipmentIn(from);
        var branchBefore = shipment.LastBranchCode;

        var ex = Should.Throw<BusinessException>(() =>
            shipment.ApplyMovement(movement, "ANK01", Now.AddHours(1)));

        ex.Code.ShouldBe(KargoAppDomainErrorCodes.InvalidShipmentStatusTransition);
        shipment.Status.ShouldBe(from);
        shipment.LastBranchCode.ShouldBe(branchBefore);
    }

    [Fact]
    public void Cancel_Created_IptalEder()
    {
        var shipment = NewShipment();

        shipment.Cancel();

        shipment.Status.ShouldBe(ShipmentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_YoldakiKargo_HataFirlatir()
    {
        var shipment = ShipmentIn(ShipmentStatus.InTransit);

        var ex = Should.Throw<BusinessException>(() => shipment.Cancel());

        ex.Code.ShouldBe(KargoAppDomainErrorCodes.ShipmentCannotBeCancelled);
        shipment.Status.ShouldBe(ShipmentStatus.InTransit);
    }
}
