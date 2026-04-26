// Core/SyncOrchestrator.cs
using System.Security.Principal;

namespace SyncLibrary.Core;

public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(string message) : base(message) { }
}