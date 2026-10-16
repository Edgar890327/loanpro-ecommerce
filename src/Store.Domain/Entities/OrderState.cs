namespace Store.Domain;

public enum OrderState
{
    Reserved,
    Paid,
    Failed,
    Expired,
    Cancelled
}
