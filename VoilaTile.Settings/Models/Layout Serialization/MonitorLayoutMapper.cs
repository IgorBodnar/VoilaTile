namespace VoilaTile.Settings.Models
{
    using VoilaTile.Common.DTO;
    using VoilaTile.Settings.ViewModels;

    /// <summary>
    /// Maps monitor layout view models to data transfer objects.
    /// </summary>
    public static class MonitorLayoutMapper
    {
        /// <summary>
        /// Maps the given monitor view models to layout DTOs.
        /// </summary>
        /// <param name="monitorViewModels">The collection of montior view models.</param>
        /// <param name="charPool">The character pool used for hint generation.</param>
        /// <returns>A layout collection dto representing the active monitor layouts.</returns>
        public static LayoutCollectionDTO MapLayouts(List<MonitorViewModel> monitorViewModels, CharacterPool charPool)
        {
            LayoutCollectionDTO layouts = new LayoutCollectionDTO();

            List<List<DividerModel>> monitorDividerCollections = new List<List<DividerModel>>();
            List<List<Tile>> monitorAtomicTileCollections = new List<List<Tile>>();
            int cumulativeAtomicTileSum = 0;

            // Extract the models from view models.
            foreach (var monitorViewModel in monitorViewModels)
            {
                var dividers = monitorViewModel.SelectedTemplate!.Template.Dividers;
                var atomicTiles = GenerateAtomicZones(dividers);
                cumulativeAtomicTileSum += atomicTiles.Count;

                monitorDividerCollections.Add(dividers);
                monitorAtomicTileCollections.Add(atomicTiles);
            }

            // Initialize hints for atomic zones.
            charPool.RefillPool(cumulativeAtomicTileSum);

            foreach (var monitorAtomicTiles in monitorAtomicTileCollections)
            {
                var monitorCharPool = charPool.DequeueMany(monitorAtomicTiles.Count);
                foreach (var tile in monitorAtomicTiles)
                {
                    tile.Hint = monitorCharPool.Dequeue();
                }
            }

            // Compute all valid tiles per monitor.
            var monitorValidTilesCollections = new List<List<Tile>>(monitorViewModels.Count);

            for (int monitorIndex = 0; monitorIndex < monitorViewModels.Count; monitorIndex++)
            {
                var validTiles = ComputeValidRectanles(
                    monitorAtomicTileCollections[monitorIndex],
                    monitorDividerCollections[monitorIndex]);

                monitorValidTilesCollections.Add(validTiles);
            }

            // Deduplicate hints across ALL monitors (in-place).
            DeduplicateHintsGlobal(monitorValidTilesCollections, charPool);

            // Emit per-monitor DTOs using the unique hints.
            for (int monitorIndex = 0; monitorIndex < monitorViewModels.Count; monitorIndex++)
            {
                var monitorInfo = monitorViewModels[monitorIndex].MonitorInfo;
                var layoutDTO = new MonitorLayoutDTO()
                {
                    MonitorID = monitorInfo.DeviceID,
                    X = monitorInfo.WorkX,
                    Y = monitorInfo.WorkY,
                    Width = monitorInfo.WorkWidth,
                    Height = monitorInfo.WorkHeight,
                    DpiX = monitorInfo.DpiX,
                    DpiY = monitorInfo.DpiY,
                };

                foreach (var tile in monitorValidTilesCollections[monitorIndex])
                {
                    layoutDTO.Tiles.Add(tile.ToDTO());
                }

                layouts.Monitors.Add(layoutDTO);
            }

            return layouts;
        }

        /// <summary>
        /// Ensures hints are unique across all monitors. Operates in-place on the provided tile lists.
        /// Keeps the first occurrence of any hint and appends characters from the shared pool
        /// to resolve subsequent collisions deterministically.
        /// </summary>
        /// <param name="tilesByMonitor">A list of tile lists, one per monitor.</param>
        /// <param name="pool">The global character pool.</param>
        private static void DeduplicateHintsGlobal(List<List<Tile>> tilesByMonitor, CharacterPool pool)
        {
            // Flatten in a deterministic order: shortest hints first, then ordinal.
            var allTiles = tilesByMonitor.SelectMany(t => t)
                .OrderBy(t => t.Hint?.Length ?? 0)
                .ThenBy(t => t.Hint, StringComparer.Ordinal)
                .ToList();

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var tile in allTiles)
            {
                string candidate = tile.Hint ?? string.Empty;

                // Append until globally unique.
                while (!seen.Add(candidate))
                {
                    if (pool.Count == 0)
                    {
                        // Top-up conservatively to limit churn on tiny pools.
                        pool.RefillPool(Math.Max(8, allTiles.Count / 4));
                    }

                    candidate += pool.Dequeue();
                }

                tile.Hint = candidate;
            }
        }

        /// <summary>
        /// Computes all valid rectangles by merging atomic zones based on the given dividers.
        /// </summary>
        /// <param name="atomicZones">The collection of tiles representing the atomic (non-merged) zones.</param>
        /// <param name="dividers">The collection of obstructing dividers.</param>
        /// <returns>A list of valid rectangles obrained by merging atomic zones.</returns>
        private static List<Tile> ComputeValidRectanles(List<Tile> atomicZones, List<DividerModel> dividers)
        {
            var zoneIndex = new Dictionary<TileAncestorSetKey, Tile>();
            var failedMerges = new HashSet<TileAncestorSetKey>();
            var validRects = new HashSet<Tile>(atomicZones);
            var newlyAdded = new Queue<Tile>(atomicZones);

            foreach (var z in atomicZones)
                zoneIndex[z.Key] = z;

            while (newlyAdded.Count > 0)
            {
                var nextBatch = new Queue<Tile>();

                foreach (var a in validRects)
                {
                    foreach (var b in newlyAdded)
                    {

                        if (a.Ancestors.Overlaps(b.Ancestors))
                        {
                            continue;
                        }

                        var combinedAncestors = a.Ancestors.Union(b.Ancestors).ToHashSet();
                        var key = new TileAncestorSetKey(combinedAncestors);

                        if (zoneIndex.ContainsKey(key) || failedMerges.Contains(key))
                        {
                            continue;
                        }

                        if (!Tile.TryMerge(a, b, dividers, out var merged))
                        {
                            failedMerges.Add(key);
                            continue;
                        }

                        merged.Hint = a.Hint + b.Hint;

                        zoneIndex[key] = merged;
                        nextBatch.Enqueue(merged);
                    }
                }

                foreach (var added in nextBatch)
                {
                    validRects.Add(added);
                }

                newlyAdded = nextBatch;
            }

            return validRects.ToList();
        }

        /// <summary>
        /// Resolves the grid cuts from the given dividers.
        /// </summary>
        /// <param name="dividers">The collection of dividers disecting the space into a grid.</param>
        /// <returns>A tuple of lists of values representing coordinates of x and y cuts.</returns>
        private static (List<double> XCuts, List<double> YCuts) ResolveGridCuts(List<DividerModel> dividers)
        {
            var xCuts = dividers
                .Where(d => d.IsVertical)
                .Select(d => d.Position)
                .Append(0.0).Append(1.0)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            var yCuts = dividers
                .Where(d => !d.IsVertical)
                .Select(d => d.Position)
                .Append(0.0).Append(1.0)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            return (xCuts, yCuts);
        }

        /// <summary>
        /// Generates atomic zones from the given dividers by exploration of obstructors.
        /// </summary>
        /// <param name="dividers">The list of dividers representing tile obstructors.</param>
        /// <returns>The list of tile representing the atomic zones.</returns>
        private static List<Tile> GenerateAtomicZones(List<DividerModel> dividers)
        {
            List<Tile> zones = new List<Tile>();

            var (xCuts, yCuts) = ResolveGridCuts(dividers);

            // Helper: checks if there's a vertical divider between col and col+1 across [boundStart, boundEnd]
            bool IsVerticallyBound(int rowIndex, int colIndex, double yStart, double yEnd)
            {
                double x = xCuts[colIndex];
                return dividers.Any(d =>
                    d.IsVertical &&
                    Math.Abs(d.Position - x) < 0.0001 &&
                    d.BoundStart <= yStart &&
                    d.BoundEnd >= yEnd);
            }

            // Helper: checks if there's a horizontal divider between row and row+1 across [boundStart, boundEnd]
            bool IsHorizontallyBound(int rowIndex, int colIndex, double xStart, double xEnd)
            {
                double y = yCuts[rowIndex];
                return dividers.Any(d =>
                    !d.IsVertical &&
                    Math.Abs(d.Position - y) < 0.0001 &&
                    d.BoundStart <= xStart &&
                    d.BoundEnd >= xEnd);
            }

            int rowCount = yCuts.Count - 1;
            int colCount = xCuts.Count - 1;

            bool[,] visited = new bool[rowCount, colCount];

            for (int col = 0; col < colCount; col++)
            {
                for (int row = 0; row < rowCount; row++)
                {
                    if (visited[row, col])
                    {
                        continue;
                    }

                    int maxRowSpan = 1;
                    int maxColSpan = 1;

                    // Try to extend column span to the right
                    for (int c = col + 1; c < colCount; c++)
                    {
                        if (IsVerticallyBound(row, c, yCuts[row], yCuts[row + 1]))
                        {
                            break;
                        }

                        bool blocked = false;
                        for (int r = row; r < row + maxRowSpan; r++)
                        {
                            if (visited[r, c])
                            {
                                blocked = true;
                                break;
                            }
                        }

                        if (blocked)
                        {
                            break;
                        }

                        maxColSpan++;
                    }

                    // Try to extend row span downwards
                    for (int r = row + 1; r < rowCount; r++)
                    {
                        if (IsHorizontallyBound(r, col, xCuts[col], xCuts[col + maxColSpan]))
                        {
                            break;
                        }

                        bool blocked = false;
                        for (int c = col; c < col + maxColSpan; c++)
                        {
                            if (visited[r, c])
                            {
                                blocked = true;
                                break;
                            }
                        }

                        if (blocked)
                        {
                            break;
                        }

                        maxRowSpan++;
                    }

                    // Mark all covered cells
                    for (int r = row; r < row + maxRowSpan; r++)
                    {
                        for (int c = col; c < col + maxColSpan; c++)
                        {
                            visited[r, c] = true;
                        }
                    }

                    var id = Guid.NewGuid();

                    var newZone = new Tile(
                        xCuts[col],
                        yCuts[row],
                        xCuts[col + maxColSpan] - xCuts[col],
                        yCuts[row + maxRowSpan] - yCuts[row],
                        new HashSet<Guid> { id });

                    zones.Add(newZone);
                }
            }

            return zones;
        }
    }
}
