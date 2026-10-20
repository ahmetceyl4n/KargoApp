using System;
using System.Collections.Generic;
using KargoApp.Shipments;

namespace KargoApp.Simulator;

// Geçiş tablosuna uyan bir okutma rotası üretir.
public static class ScanFactory
{
    // Rota: (Çıkış → Varış) × aktarma sayısı, sonra Dağıtıma çıkış → Teslim
    public static List<ScanEto> Create(string trackingNumber, int movementCount,
                                       IReadOnlyList<string> branchCodes, DateTime start)
    {
        var hops = Math.Max(1, (movementCount - 2) / 2);
        var scans = new List<ScanEto>();
        var time = start;
        var branch = Pick(branchCodes);

        for (var i = 0; i < hops; i++)
        {
            scans.Add(New(trackingNumber, branch, MovementType.DepartedBranch, time = time.AddHours(1)));
            branch = Pick(branchCodes);
            scans.Add(New(trackingNumber, branch, MovementType.ArrivedAtBranch, time = time.AddHours(1)));
        }

        scans.Add(New(trackingNumber, branch, MovementType.OutForDelivery, time = time.AddHours(1)));
        scans.Add(New(trackingNumber, branch, MovementType.Delivered, time.AddHours(1)));
        return scans;
    }

    private static string Pick(IReadOnlyList<string> codes) => codes[Random.Shared.Next(codes.Count)];

    private static ScanEto New(string trackingNumber, string branch, MovementType type, DateTime at) =>
        new()
        {
            ScanId = Guid.NewGuid(),
            TrackingNumber = trackingNumber,
            BranchCode = branch,
            MovementType = type,
            OccurredAt = at
        };
}
