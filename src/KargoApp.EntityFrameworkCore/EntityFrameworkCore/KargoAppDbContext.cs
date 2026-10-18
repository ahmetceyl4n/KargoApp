using KargoApp.Branches;
using KargoApp.Shipments;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;

namespace KargoApp.EntityFrameworkCore;

[ReplaceDbContext(typeof(IIdentityDbContext))]
[ConnectionStringName("Default")]
public class KargoAppDbContext :
    AbpDbContext<KargoAppDbContext>,
    IIdentityDbContext
{
    /* Add DbSet properties for your Aggregate Roots / Entities here. */
    public DbSet<Shipment> Shipments { get; set; }
    public DbSet<Branch> Branches { get; set; }
    public DbSet<ShipmentMovement> ShipmentMovements { get; set; }

    #region Entities from the modules

    /* Notice: We only implemented IIdentityProDbContext 
     * and replaced them for this DbContext. This allows you to perform JOIN
     * queries for the entities of these modules over the repositories easily. You
     * typically don't need that for other modules. But, if you need, you can
     * implement the DbContext interface of the needed module and use ReplaceDbContext
     * attribute just like IIdentityProDbContext .
     *
     * More info: Replacing a DbContext of a module ensures that the related module
     * uses this DbContext on runtime. Otherwise, it will use its own DbContext class.
     */

    // Identity
    public DbSet<IdentityUser> Users { get; set; }
    public DbSet<IdentityRole> Roles { get; set; }
    public DbSet<IdentityClaimType> ClaimTypes { get; set; }
    public DbSet<OrganizationUnit> OrganizationUnits { get; set; }
    public DbSet<IdentitySecurityLog> SecurityLogs { get; set; }
    public DbSet<IdentityLinkUser> LinkUsers { get; set; }
    public DbSet<IdentityUserDelegation> UserDelegations { get; set; }
    public DbSet<IdentitySession> Sessions { get; set; }

    #endregion

    public KargoAppDbContext(DbContextOptions<KargoAppDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        /* Include modules to your migration db context */

        builder.ConfigurePermissionManagement();
        builder.ConfigureSettingManagement();
        builder.ConfigureBackgroundJobs();
        builder.ConfigureAuditLogging();
        builder.ConfigureFeatureManagement();
        builder.ConfigureIdentity();
        builder.ConfigureOpenIddict();
        builder.ConfigureBlobStoring();

        /* Configure your own tables/entities inside here */

        builder.Entity<Shipment>(b =>
        {
            b.ToTable(KargoAppConsts.DbTablePrefix + "Shipments", KargoAppConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props

            b.Property(x => x.TrackingNumber).IsRequired().HasMaxLength(ShipmentConsts.TrackingNumberLength);
            b.Property(x => x.SenderName).IsRequired().HasMaxLength(ShipmentConsts.MaxNameLength);
            b.Property(x => x.ReceiverName).IsRequired().HasMaxLength(ShipmentConsts.MaxNameLength);
            b.Property(x => x.ReceiverCity).IsRequired().HasMaxLength(ShipmentConsts.MaxCityLength);
            b.Property(x => x.WeightKg).HasPrecision(6, 2);
            b.Property(x => x.LastBranchCode).HasMaxLength(BranchConsts.MaxCodeLength);

            b.HasIndex(x => x.TrackingNumber).IsUnique();
        });

        builder.Entity<Branch>(b =>
        {
            b.ToTable(KargoAppConsts.DbTablePrefix + "Branches", KargoAppConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasColumnName("Code").HasMaxLength(BranchConsts.MaxCodeLength);
            b.Property(x => x.Name).IsRequired().HasMaxLength(BranchConsts.MaxNameLength);
            b.Property(x => x.City).IsRequired().HasMaxLength(BranchConsts.MaxCityLength);
        });

        builder.Entity<ShipmentMovement>(b =>
        {
            b.ToTable(KargoAppConsts.DbTablePrefix + "ShipmentMovements", KargoAppConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.BranchCode).IsRequired().HasMaxLength(BranchConsts.MaxCodeLength);

            b.HasIndex(x => x.ScanId).IsUnique();                 // aynı okutma iki kez yazılamaz
            b.HasIndex(x => new { x.ShipmentId, x.OccurredAt });  // geçmiş sorgusu: WHERE ShipmentId ORDER BY OccurredAt

            // Navigation property yok (Shipment.Movements tutmuyoruz), sadece veritabanı kısıtı.
            b.HasOne<Shipment>().WithMany()
             .HasForeignKey(x => x.ShipmentId)
             .IsRequired();
        });
    }
}
