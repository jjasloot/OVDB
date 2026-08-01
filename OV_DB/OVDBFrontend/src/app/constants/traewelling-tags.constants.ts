/**
 * Standard Traewelling tag keys as defined in the Traewelling API
 * Source: https://github.com/Traewelling/traewelling/blob/4ab773cadd67f65b00c8499daaab72b3569b3ea7/app/Enum/StatusTagKey.php
 *
 * Readable labels live in the translation files under TRAEWELLING.TAG.* and are
 * resolved by TrawellingService.formatTagKey().
 */
export const STANDARD_TRAEWELLING_TAGS = [
  'trwl:seat',
  'trwl:wagon',
  'trwl:ticket',
  'trwl:travel_class',
  'trwl:locomotive_class',
  'trwl:wagon_class',
  'trwl:role',
  'trwl:vehicle_number',
  'trwl:passenger_rights',
  'trwl:journey_number',
  'trwl:price'
] as const;
