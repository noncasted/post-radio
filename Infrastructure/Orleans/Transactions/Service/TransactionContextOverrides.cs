using System.Reflection;
using Orleans.Transactions;

namespace Infrastructure.Orleans;

public static class TransactionContextOverrides {
    private static readonly Type TransactionContextType = typeof(TransactionContext);

    private static readonly MethodInfo SetTransactionInfoMethod =
        TransactionContextType.GetMethod("SetTransactionInfo", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo ClearMethod =
        TransactionContextType.GetMethod("Clear", BindingFlags.NonPublic | BindingFlags.Static)!;

    public static void SetTransactionInfo(TransactionInfo info) {
        if (SetTransactionInfoMethod == null) {
            throw new InvalidOperationException("SetTransactionInfo method not found.");
        }

        SetTransactionInfoMethod.Invoke(null, new object[] { info });
    }

    public static void Clear() {
        if (ClearMethod == null) {
            throw new InvalidOperationException("Clear method not found.");
        }

        ClearMethod.Invoke(null, null);
    }
}