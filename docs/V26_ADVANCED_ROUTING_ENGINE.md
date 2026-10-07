# V26 Advanced Routing Engine

This update closes the RFP gap under **Routing Engine** by extending legacy BIN routing to support Tier-1 bank switch route selection criteria.

## Added route criteria

A route can now be matched by the following optional dimensions:

- Country code
- Merchant Category Code (MCC)
- Currency code
- Device / terminal code
- Interchange code
- Card range / BIN range
- Institution code
- Product code
- Network code
- Account number / account range

Blank criteria are treated as wildcards, so existing BIN-only routing continues to work.

## ISO 8583 field mapping used by the processor

- Card/PAN: field 2
- Processing code: field 3
- MCC: field 18
- Country: field 19, fallback from merchant location field 43 suffix
- Network/interchange: fields 24, 32, 33, 100, 123 depending on message profile
- Device: field 41
- Currency: field 49
- Institution: source node institution, field 32, field 33, or field 100
- Product: field 120 or field 123
- Account number: field 102 or field 103
- Channel: field 123 positions 14-15, fallback field 22/25

## Selection order

The repository returns the best active route by:

1. Route match against all populated criteria.
2. Highest specificity score.
3. Highest route priority.
4. Longest card-range/BIN match.
5. Stable BIN ordering.

## Database change

Migration `db/019_advanced_routing_criteria.sql` adds the route criteria columns and drops the old unique active-BIN index, because multiple active routes can now share a BIN prefix and differ by MCC, country, currency, network, institution, device, product, or account range.

## Admin portal

The route configuration page now supports maintaining all advanced criteria with semicolon-separated values. Range criteria support either prefix values or inclusive ranges such as `400000-400999`.
