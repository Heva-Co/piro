import { describe, it, expect } from "vitest";
import { isValidIpOrHostname, isValidHostname, isValidIpv4, isValidIpv6 } from "./validations";

describe("isValidIpv4", () => {
  it.each(["8.8.8.8", "0.0.0.0", "255.255.255.255", "35.244.151.1"])("accepts %s", (v) => {
    expect(isValidIpv4(v)).toBe(true);
  });
  it.each(["256.1.1.1", "1.2.3", "1.2.3.4.5", "01.2.3.4", "1.2.3.256", "a.b.c.d"])("rejects %s", (v) => {
    expect(isValidIpv4(v)).toBe(false);
  });
});

describe("isValidIpv6", () => {
  it.each([
    "2001:4860:4860::8888",
    "::1",
    "::",
    "fe80::1",
    "2001:db8:0:0:0:0:0:1",
  ])("accepts %s", (v) => {
    expect(isValidIpv6(v)).toBe(true);
  });
  it.each([
    "gggg::1",        // non-hex
    "1::2::3",        // two ::
    "12345::1",       // hextet too long
    "1.2.3.4",        // not IPv6
    ":::",            // garbage
  ])("rejects %s", (v) => {
    expect(isValidIpv6(v)).toBe(false);
  });
});

describe("isValidHostname — RFC 1123/1035", () => {
  it.each([
    "dns.google",
    "ns1.example.com",
    "localhost",
    "a",
    "asdasd-asdasda",   // single label with internal hyphen — valid
    "example.com.",     // trailing dot tolerated
    "8x8.com",          // leading digit label (RFC 1123)
  ])("accepts %s", (v) => {
    expect(isValidHostname(v)).toBe(true);
  });

  it.each([
    "foo_bar",          // underscore not allowed
    "-foo",             // label starts with hyphen
    "foo-",             // label ends with hyphen
    "foo.-bar.com",     // a label starts with hyphen
    "bad host",         // space
    "foo.123",          // numeric TLD on a multi-label name
    "a".repeat(64),     // label > 63 chars
    "a".repeat(254),    // total > 253
  ])("rejects %s", (v) => {
    expect(isValidHostname(v)).toBe(false);
  });
});

describe("isValidIpOrHostname — dispatch", () => {
  it("routes IPv4 / IPv6 / hostname correctly", () => {
    expect(isValidIpOrHostname("8.8.8.8")).toBe(true);
    expect(isValidIpOrHostname("2001:db8::1")).toBe(true);
    expect(isValidIpOrHostname("dns.google")).toBe(true);
    expect(isValidIpOrHostname("asdasd-*/798243465r434")).toBe(false);
    expect(isValidIpOrHostname("")).toBe(false);
  });
});
