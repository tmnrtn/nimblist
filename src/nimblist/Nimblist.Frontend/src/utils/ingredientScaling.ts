// ---------------------------------------------------------------------------
// Quantity parsing
// ---------------------------------------------------------------------------

const UNICODE_FRACTIONS: Record<string, number> = {
  '½': 0.5, '⅓': 1/3, '⅔': 2/3, '¼': 0.25, '¾': 0.75,
  '⅛': 0.125, '⅜': 0.375, '⅝': 0.625, '⅞': 0.875,
};

// Regex: optional whole number, optional fraction (n/d or unicode), then unit text
const QTY_RE = /^(\d+)?\s*(\d+\/\d+|[½⅓⅔¼¾⅛⅜⅝⅞])?\s*(.*)/;
// Range: e.g. "2-3 cups" or "2 to 3 cups"
const RANGE_RE = /^(\d+(?:\.\d+)?)\s*(?:-|to)\s*(\d+(?:\.\d+)?)\s*(.*)/i;

interface ParsedQty {
  amount: number;
  unit: string;
}

export function parseQuantity(str: string | null | undefined): ParsedQty | null {
  if (!str?.trim()) return null;
  const s = str.trim();

  // Range: take the average
  const rangeMatch = RANGE_RE.exec(s);
  if (rangeMatch) {
    const lo = parseFloat(rangeMatch[1]);
    const hi = parseFloat(rangeMatch[2]);
    return { amount: (lo + hi) / 2, unit: rangeMatch[3].trim() };
  }

  const m = QTY_RE.exec(s);
  if (!m) return null;

  const [, wholeStr, fracStr, unitStr] = m;
  if (!wholeStr && !fracStr) return null;

  let amount = 0;
  if (wholeStr) amount += parseInt(wholeStr, 10);
  if (fracStr) {
    if (fracStr in UNICODE_FRACTIONS) {
      amount += UNICODE_FRACTIONS[fracStr];
    } else {
      const [num, den] = fracStr.split('/').map(Number);
      if (den) amount += num / den;
    }
  }

  if (amount === 0) return null;
  return { amount, unit: unitStr.trim() };
}

// ---------------------------------------------------------------------------
// Amount formatting — prefer unicode fractions for common values
// ---------------------------------------------------------------------------

const DISPLAY_FRACTIONS: [number, string][] = [
  [1/8, '⅛'], [1/4, '¼'], [1/3, '⅓'], [3/8, '⅜'],
  [1/2, '½'], [5/8, '⅝'], [2/3, '⅔'], [3/4, '¾'], [7/8, '⅞'],
];

export function formatAmount(n: number): string {
  if (n <= 0) return '0';
  const whole = Math.floor(n);
  const frac = n - whole;

  let fracStr = '';
  for (const [val, sym] of DISPLAY_FRACTIONS) {
    if (Math.abs(frac - val) < 0.04) { fracStr = sym; break; }
  }

  if (fracStr) return whole > 0 ? `${whole} ${fracStr}` : fracStr;

  // No clean fraction — use decimal, rounding to sensible precision
  if (n >= 100) return String(Math.round(n));
  if (n >= 10) return (Math.round(n * 2) / 2).toString(); // nearest 0.5
  if (n >= 1) return parseFloat(n.toFixed(1)).toString();
  return parseFloat(n.toFixed(2)).toString();
}

// ---------------------------------------------------------------------------
// Unit conversion — imperial → metric
// ---------------------------------------------------------------------------

interface Conversion { factor: number; metric: string }

const CONVERSIONS: Record<string, Conversion> = {
  // Volume
  tsp:             { factor: 4.92,  metric: 'ml' },
  teaspoon:        { factor: 4.92,  metric: 'ml' },
  teaspoons:       { factor: 4.92,  metric: 'ml' },
  tbsp:            { factor: 14.79, metric: 'ml' },
  tablespoon:      { factor: 14.79, metric: 'ml' },
  tablespoons:     { factor: 14.79, metric: 'ml' },
  'fl oz':         { factor: 29.57, metric: 'ml' },
  'fluid ounce':   { factor: 29.57, metric: 'ml' },
  'fluid ounces':  { factor: 29.57, metric: 'ml' },
  cup:             { factor: 240,   metric: 'ml' },
  cups:            { factor: 240,   metric: 'ml' },
  pint:            { factor: 473,   metric: 'ml' },
  pints:           { factor: 473,   metric: 'ml' },
  pt:              { factor: 473,   metric: 'ml' },
  quart:           { factor: 946,   metric: 'ml' },
  quarts:          { factor: 946,   metric: 'ml' },
  qt:              { factor: 946,   metric: 'ml' },
  gallon:          { factor: 3785,  metric: 'ml' },
  gallons:         { factor: 3785,  metric: 'ml' },
  gal:             { factor: 3785,  metric: 'ml' },
  // Weight
  oz:              { factor: 28.35, metric: 'g' },
  ounce:           { factor: 28.35, metric: 'g' },
  ounces:          { factor: 28.35, metric: 'g' },
  lb:              { factor: 453.6, metric: 'g' },
  lbs:             { factor: 453.6, metric: 'g' },
  pound:           { factor: 453.6, metric: 'g' },
  pounds:          { factor: 453.6, metric: 'g' },
  // Length
  inch:            { factor: 2.54,  metric: 'cm' },
  inches:          { factor: 2.54,  metric: 'cm' },
  '"':             { factor: 2.54,  metric: 'cm' },
};

function upgradeUnit(amount: number, unit: string): [number, string] {
  if (unit === 'ml' && amount >= 1000) return [amount / 1000, 'L'];
  if (unit === 'g'  && amount >= 1000) return [amount / 1000, 'kg'];
  return [amount, unit];
}

export function isImperialUnit(unit: string): boolean {
  return unit.toLowerCase() in CONVERSIONS;
}

export function hasAnyImperialUnit(quantities: (string | null | undefined)[]): boolean {
  return quantities.some(q => {
    const parsed = parseQuantity(q);
    return parsed ? isImperialUnit(parsed.unit) : false;
  });
}

// ---------------------------------------------------------------------------
// Main transform: scale + optional metric conversion
// ---------------------------------------------------------------------------

export function transformQuantity(
  original: string | null | undefined,
  scaleFactor: number,
  convertMetric: boolean,
): string | null {
  if (!original?.trim()) return original ?? null;

  const parsed = parseQuantity(original);
  if (!parsed) return original; // unparseable — return as-is

  let { amount, unit } = parsed;
  amount *= scaleFactor;

  const conv = CONVERSIONS[unit.toLowerCase()];
  if (convertMetric && conv) {
    amount = amount * conv.factor;
    unit = conv.metric;
    [amount, unit] = upgradeUnit(amount, unit);
    // Round metric values sensibly
    if (unit === 'ml' || unit === 'g') amount = Math.round(amount);
    else if (unit === 'L' || unit === 'kg') amount = Math.round(amount * 10) / 10;
    else amount = Math.round(amount * 10) / 10;
    return unit ? `${formatAmount(amount)} ${unit}` : formatAmount(amount);
  }

  const formatted = formatAmount(amount);
  return unit ? `${formatted} ${unit}` : formatted;
}
