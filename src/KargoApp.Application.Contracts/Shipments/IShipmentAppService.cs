using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace KargoApp.Shipments;

public interface IShipmentAppService : IApplicationService
{
    Task<ShipmentDto> CreateAsync(CreateShipmentDto input);

    Task<ShipmentDto> GetByTrackingNumberAsync(string trackingNumber);

    Task<ShipmentDto> CancelAsync(string trackingNumber);
}
