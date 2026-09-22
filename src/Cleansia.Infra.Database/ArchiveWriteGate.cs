using Cleansia.Core.Domain.Tenancy;

namespace Cleansia.Infra.Database;

/// <summary>
/// Scoped, closed by default; open while any legal-obligation scope a caller took is undisposed.
/// </summary>
public sealed class ArchiveWriteGate : IArchiveWriteGate
{
    private int _openings;

    public bool IsOpen => _openings > 0;

    public IDisposable OpenForLegalObligation(string reason)
    {
        _openings++;
        return new Opening(this);
    }

    private sealed class Opening(ArchiveWriteGate gate) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            gate._openings--;
        }
    }
}
