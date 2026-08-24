using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace ReactorSim.Core
{
    /// <summary>
    /// A stable, machine-readable failure returned by a contract loader.
    /// </summary>
    public sealed class ContractDiagnostic
    {
        public ContractDiagnostic(string code, string path, string message)
        {
            Code = RequireText(code, nameof(code));
            Path = RequireText(path, nameof(path));
            Message = RequireText(message, nameof(message));
        }

        public string Code { get; }

        public string Path { get; }

        public string Message { get; }

        public override string ToString()
        {
            return Code + " at " + Path + ": " + Message;
        }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("The diagnostic text must not be empty.", parameterName);
            }

            return value;
        }
    }

    /// <summary>
    /// Result of a fail-closed contract load. Invalid results contain exactly
    /// one deterministic first diagnostic; later gates may add richer reports
    /// without changing the authoritative first-failure behavior.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification = "Typed factories keep fail-closed results explicit at each contract call site.")]
    public sealed class ContractValidationResult<T>
    {
        private ContractValidationResult(bool isValid, T value, IReadOnlyList<ContractDiagnostic> diagnostics)
        {
            IsValid = isValid;
            Value = value;
            Diagnostics = diagnostics;
        }

        public bool IsValid { get; }

        public T Value { get; }

        public IReadOnlyList<ContractDiagnostic> Diagnostics { get; }

        public ContractDiagnostic FirstDiagnostic
        {
            get
            {
                if (IsValid)
                {
                    throw new InvalidOperationException("A valid result has no diagnostic.");
                }

                return Diagnostics[0];
            }
        }

        public static ContractValidationResult<T> Valid(T value)
        {
            return new ContractValidationResult<T>(
                true,
                value,
                new ReadOnlyCollection<ContractDiagnostic>(Array.Empty<ContractDiagnostic>()));
        }

        public static ContractValidationResult<T> Invalid(string code, string path, string message)
        {
            return new ContractValidationResult<T>(
                false,
                default(T)!,
                new ReadOnlyCollection<ContractDiagnostic>(
                    new[] { new ContractDiagnostic(code, path, message) }));
        }
    }

    internal static class ContractValidation
    {
        public static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        public static string ChannelPath(ChannelId channelId, string suffix)
        {
            return "channels[" + channelId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]" + suffix;
        }

        public static string NodePath(NodeKey nodeKey, string suffix)
        {
            return "nodes[channel=" + nodeKey.ChannelId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                   ",position=" + nodeKey.Position.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]" + suffix;
        }
    }
}
