/**
 * Standard Traewelling tag keys as defined in the Traewelling API
 * Source: https://github.com/Traewelling/traewelling/blob/4ab773cadd67f65b00c8499daaab72b3569b3ea7/app/Enum/StatusTagKey.php
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

/**
 * Human-readable labels for standard Traewelling tags
 */
export const TRAEWELLING_TAG_LABELS: Record<string, string> = {
  'trwl:seat': 'Seat Number',
  'trwl:wagon': 'Wagon/Coach',
  'trwl:ticket': 'Ticket Type',
  'trwl:travel_class': 'Travel Class',
  'trwl:locomotive_class': 'Locomotive Class',
  'trwl:wagon_class': 'Wagon Class',
  'trwl:role': 'Role (e.g., conductor)',
  'trwl:vehicle_number': 'Vehicle Number',
  'trwl:passenger_rights': 'Passenger Rights',
  'trwl:journey_number': 'Journey Number',
  'trwl:price': 'Price'
};
