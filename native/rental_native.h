#ifndef RENTAL_NATIVE_H
#define RENTAL_NATIVE_H

#ifdef _WIN32
#define RENTAL_API __declspec(dllexport)
#else
#define RENTAL_API
#endif

RENTAL_API long long calculate_service_fee(
    int quantity,
    long long fee_per_item_cents
);

#endif