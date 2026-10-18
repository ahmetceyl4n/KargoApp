using System;
using System.ComponentModel.DataAnnotations;
using KargoApp.Branches;

namespace KargoApp.Shipments;

public class AddMovementDto
{
    // Nullable: alan hiç gönderilmezse Guid.Empty / 0001-01-01 / 0 olarak sessizce kabul edilmesin, [Required] yakalasın.
    [Required]
    public Guid? ScanId { get; set; }

    [Required]
    [StringLength(BranchConsts.MaxCodeLength)]
    public string BranchCode { get; set; } = default!;

    [Required]
    [EnumDataType(typeof(MovementType))]
    public MovementType? MovementType { get; set; }

    [Required]
    public DateTime? OccurredAt { get; set; }
}
