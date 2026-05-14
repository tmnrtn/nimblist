import { describe, it, expect } from 'vitest';
import { parseQuantity, formatAmount, transformQuantity } from './ingredientScaling';

// ---------------------------------------------------------------------------
// parseQuantity
// ---------------------------------------------------------------------------

describe('parseQuantity', () => {
  it('returns null for empty / null / undefined input', () => {
    expect(parseQuantity(null)).toBeNull();
    expect(parseQuantity(undefined)).toBeNull();
    expect(parseQuantity('')).toBeNull();
    expect(parseQuantity('   ')).toBeNull();
  });

  it('parses a whole number', () => {
    expect(parseQuantity('2 cups')).toEqual({ amount: 2, unit: 'cups' });
    expect(parseQuantity('10 g')).toEqual({ amount: 10, unit: 'g' });
    expect(parseQuantity('1')).toEqual({ amount: 1, unit: '' });
  });

  it('parses a decimal', () => {
    expect(parseQuantity('2.5 tsp')).toEqual({ amount: 2.5, unit: 'tsp' });
    expect(parseQuantity('0.5 cup')).toEqual({ amount: 0.5, unit: 'cup' });
  });

  it('parses a mixed number (whole + fraction)', () => {
    expect(parseQuantity('1 1/2 cups')).toEqual({ amount: 1.5, unit: 'cups' });
    expect(parseQuantity('2 3/4 tsp')).toEqual({ amount: 2.75, unit: 'tsp' });
  });

  it('parses an improper fraction without treating the numerator as whole', () => {
    expect(parseQuantity('3/2 cups')).toEqual({ amount: 1.5, unit: 'cups' });
    expect(parseQuantity('1/4 tsp')).toEqual({ amount: 0.25, unit: 'tsp' });
    expect(parseQuantity('3/4 cup')).toEqual({ amount: 0.75, unit: 'cup' });
  });

  it('parses a unicode fraction alone', () => {
    expect(parseQuantity('½ cup')).toEqual({ amount: 0.5, unit: 'cup' });
    expect(parseQuantity('¼ tsp')).toEqual({ amount: 0.25, unit: 'tsp' });
    expect(parseQuantity('⅔ cup')).toEqual({ amount: expect.closeTo(2 / 3, 5), unit: 'cup' });
  });

  it('parses whole + unicode fraction', () => {
    expect(parseQuantity('1½ cups')).toEqual({ amount: 1.5, unit: 'cups' });
    expect(parseQuantity('2¾ lbs')).toEqual({ amount: 2.75, unit: 'lbs' });
  });

  it('parses a range by taking the average', () => {
    expect(parseQuantity('2-3 cups')).toEqual({ amount: 2.5, unit: 'cups' });
    expect(parseQuantity('1 to 2 tsp')).toEqual({ amount: 1.5, unit: 'tsp' });
  });

  it('returns null for unparseable strings', () => {
    expect(parseQuantity('a handful')).toBeNull();
    expect(parseQuantity('to taste')).toBeNull();
  });

  it('returns null for a string starting with a slash', () => {
    // Edge case: "/ something" should not parse as a fraction
    expect(parseQuantity('/2 cups')).toBeNull();
  });
});

// ---------------------------------------------------------------------------
// formatAmount
// ---------------------------------------------------------------------------

describe('formatAmount', () => {
  it('renders whole numbers as integers', () => {
    expect(formatAmount(1)).toBe('1');
    expect(formatAmount(3)).toBe('3');
  });

  it('renders common fractions as unicode symbols', () => {
    expect(formatAmount(0.5)).toBe('½');
    expect(formatAmount(0.25)).toBe('¼');
    expect(formatAmount(0.75)).toBe('¾');
  });

  it('renders mixed numbers with a unicode fraction', () => {
    expect(formatAmount(1.5)).toBe('1 ½');
    expect(formatAmount(2.25)).toBe('2 ¼');
  });
});

// ---------------------------------------------------------------------------
// transformQuantity — the full pipeline
// ---------------------------------------------------------------------------

describe('transformQuantity', () => {
  it('returns null / empty input unchanged', () => {
    expect(transformQuantity(null, 1, false)).toBeNull();
    expect(transformQuantity('', 1, false)).toBe('');
  });

  it('returns unparseable strings unchanged', () => {
    expect(transformQuantity('to taste', 1, false)).toBe('to taste');
    expect(transformQuantity('a pinch', 1, false)).toBe('a pinch');
  });

  it('normalises an improper fraction at scale 1×', () => {
    expect(transformQuantity('3/2 cups', 1, false)).toBe('1 ½ cups');
    expect(transformQuantity('3/4 cup', 1, false)).toBe('¾ cup');
  });

  it('scales a mixed-number string correctly', () => {
    expect(transformQuantity('1 1/2 cups', 2, false)).toBe('3 cups');
  });

  it('scales an improper fraction correctly (the original bug)', () => {
    // "3/2 cups" is 1.5 cups; ×2 should give "3 cups", not "6/2 cups"
    expect(transformQuantity('3/2 cups', 2, false)).toBe('3 cups');
  });

  it('scales a whole number', () => {
    expect(transformQuantity('2 cups', 2, false)).toBe('4 cups');
    expect(transformQuantity('3 tbsp', 0.5, false)).toBe('1 ½ tbsp');
  });

  it('scales a unicode fraction', () => {
    expect(transformQuantity('½ cup', 2, false)).toBe('1 cup');
    expect(transformQuantity('¼ tsp', 4, false)).toBe('1 tsp');
  });

  it('handles a quantity with no unit', () => {
    expect(transformQuantity('3', 2, false)).toBe('6');
    expect(transformQuantity('1 1/2', 2, false)).toBe('3');
  });

  it('converts imperial to metric when convertMetric is true', () => {
    // 1 cup = 240 ml
    expect(transformQuantity('1 cup', 1, true)).toBe('240 ml');
    // 2 cups = 480 ml
    expect(transformQuantity('2 cups', 1, true)).toBe('480 ml');
  });

  it('upgrades ml to L when over 1000', () => {
    // 5 cups = 1200 ml → 1.2 L
    expect(transformQuantity('5 cups', 1, true)).toBe('1.2 L');
  });
});
