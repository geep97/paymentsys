namespace PaymentSys.Middleware
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class IdempotentAttribute : Attribute
    {
    }
}
