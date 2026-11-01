#include "rental_native.h"
#include <limits.h>

long long calculate_service_fee(
    int quantity,
    long long fee_per_item_cents
)
{
    if (quantity < 0 || fee_per_item_cents < 0)
    {
        return -1;
    }

    // Protect against integer overflow.
    if (quantity != 0 &&
        fee_per_item_cents > LLONG_MAX / quantity)
    {
        return -1;
    }

    return (long long)quantity * fee_per_item_cents;
}