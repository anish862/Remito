using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Remito.Domain.Entities;

namespace Remito.Infrastructure.Persistence.Configurations;

internal sealed class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.HasKey(currency => currency.Code);
        builder.Property(currency => currency.Code).HasMaxLength(3).IsFixedLength();
        builder.Property(currency => currency.Name).HasMaxLength(100).IsRequired();
        builder.Property(currency => currency.Symbol).HasMaxLength(10).IsRequired();
        builder.HasData(
            new Currency { Code = "OMR", Name = "Omani Rial", Symbol = "ر.ع.", DecimalPlaces = 3 },
            new Currency { Code = "INR", Name = "Indian Rupee", Symbol = "₹", DecimalPlaces = 2 },
            new Currency { Code = "USD", Name = "United States Dollar", Symbol = "$", DecimalPlaces = 2 },
            new Currency { Code = "AED", Name = "United Arab Emirates Dirham", Symbol = "د.إ", DecimalPlaces = 2 },
            new Currency { Code = "SAR", Name = "Saudi Riyal", Symbol = "ر.س", DecimalPlaces = 2 },
            new Currency { Code = "PKR", Name = "Pakistani Rupee", Symbol = "₨", DecimalPlaces = 2 },
            new Currency { Code = "BDT", Name = "Bangladeshi Taka", Symbol = "৳", DecimalPlaces = 2 },
            new Currency { Code = "PHP", Name = "Philippine Peso", Symbol = "₱", DecimalPlaces = 2 },
            new Currency { Code = "NPR", Name = "Nepalese Rupee", Symbol = "रू", DecimalPlaces = 2 },
            new Currency { Code = "LKR", Name = "Sri Lankan Rupee", Symbol = "රු", DecimalPlaces = 2 });
    }
}

internal sealed class CorridorConfiguration : IEntityTypeConfiguration<Corridor>
{
    private static readonly Guid OmaniRialToIndianRupeeId =
        new("0f43b2c9-44b3-4b91-8e7f-2aa9e19ecb01");

    public void Configure(EntityTypeBuilder<Corridor> builder)
    {
        builder.HasKey(corridor => corridor.Id);
        builder.Property(corridor => corridor.FromCurrency).HasMaxLength(3).IsFixedLength();
        builder.Property(corridor => corridor.ToCurrency).HasMaxLength(3).IsFixedLength();
        builder.HasIndex(corridor => new { corridor.FromCurrency, corridor.ToCurrency }).IsUnique();
        builder.HasOne(corridor => corridor.FromCurrencyEntity).WithMany()
            .HasForeignKey(corridor => corridor.FromCurrency).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(corridor => corridor.ToCurrencyEntity).WithMany()
            .HasForeignKey(corridor => corridor.ToCurrency).OnDelete(DeleteBehavior.Restrict);
        builder.HasData(new Corridor
        {
            Id = OmaniRialToIndianRupeeId,
            FromCurrency = "OMR",
            ToCurrency = "INR",
            IsActive = true
        });
    }
}
