import { describe, it, expect } from "vitest";
import { validateConfig } from "./validators";

type Field = { key: string; required: boolean; validator?: string | null; label: string };

const nameServers: Field = { key: "nameServers", required: false, validator: "ipOrHostname", label: "Name servers" };
const statusCodes: Field = { key: "expectedStatusCodes", required: false, validator: "statusCodes", label: "Expected status codes" };
const host: Field = { key: "host", required: true, validator: "ipOrHostname", label: "Host" };
const port: Field = { key: "port", required: false, validator: "port", label: "Port" };

const IP_HOST_MSG = "Enter a valid IP address or hostname.";

describe("validateConfig — per-item list validation (nameServers / ipOrHostname)", () => {
  it("A1: all-valid IPv4 list → no error", () => {
    const errors = validateConfig([nameServers], { nameServers: ["8.8.8.8", "1.1.1.1"] });
    expect(errors.nameServers).toBeUndefined();
  });

  it("A2: all-valid hostnames → no error", () => {
    const errors = validateConfig([nameServers], { nameServers: ["dns.google", "ns1.example.com"] });
    expect(errors.nameServers).toBeUndefined();
  });

  it("A3: one bad entry → error only at the offending index", () => {
    const errors = validateConfig([nameServers], { nameServers: ["8.8.8.8", "foo_bar", "1.1.1.1"] });
    expect(errors.nameServers).toEqual({ 1: IP_HOST_MSG });
  });

  it("A4: multiple bad entries → a map with each bad index", () => {
    const errors = validateConfig([nameServers], { nameServers: ["foo_bar", "8.8.8.8", "bad host"] });
    expect(errors.nameServers).toEqual({ 0: IP_HOST_MSG, 2: IP_HOST_MSG });
  });

  it("A5: empty list → no error (system resolver)", () => {
    const errors = validateConfig([nameServers], { nameServers: [] });
    expect(errors.nameServers).toBeUndefined();
  });

  it("A6: a blank entry is not a format error (ipOrHostname treats empty as 'presence handled elsewhere')", () => {
    // The ipOrHostname validator returns null for blank input by design (its own comment: presence is
    // handled by `required`, not by the format check). So a blank list item produces no per-item error.
    const errors = validateConfig([nameServers], { nameServers: ["8.8.8.8", ""] });
    expect(errors.nameServers).toBeUndefined();
  });

  it("A7: valid IPv6 → no error", () => {
    const errors = validateConfig([nameServers], { nameServers: ["2001:4860:4860::8888"] });
    expect(errors.nameServers).toBeUndefined();
  });

  it("A8: IPv4 octet out of range → error at index", () => {
    const errors = validateConfig([nameServers], { nameServers: ["256.1.1.1"] });
    expect(errors.nameServers).toEqual({ 0: IP_HOST_MSG });
  });

  it("A9: absent field, not required → no error", () => {
    const errors = validateConfig([nameServers], {});
    expect(errors.nameServers).toBeUndefined();
  });
});

describe("validateConfig — statusCodes stays list-aware (non-regression)", () => {
  it("B1: valid codes/classes on the whole list → no error", () => {
    const errors = validateConfig([statusCodes], { expectedStatusCodes: ["200", "2xx"] });
    expect(errors.expectedStatusCodes).toBeUndefined();
  });

  it("B2: invalid list → a single string message, NOT a per-item map", () => {
    const errors = validateConfig([statusCodes], { expectedStatusCodes: ["999"] });
    expect(typeof errors.expectedStatusCodes).toBe("string");
    expect(errors.expectedStatusCodes).toBe("Use codes or classes like 200 or 2xx.");
  });

  it("B3: empty list → no error", () => {
    const errors = validateConfig([statusCodes], { expectedStatusCodes: [] });
    expect(errors.expectedStatusCodes).toBeUndefined();
  });
});

describe("validateConfig — scalar validators unchanged (non-regression)", () => {
  it("C1: ipOrHostname on a scalar field → string, not a map", () => {
    const errors = validateConfig([host], { host: "foo_bar" });
    expect(errors.host).toBe(IP_HOST_MSG);
  });

  it("C1b: required scalar empty → required message", () => {
    const errors = validateConfig([host], { host: "" });
    expect(errors.host).toBe("Host is required.");
  });

  it("C3: port out of range → string message", () => {
    const errors = validateConfig([port], { port: 70000 });
    expect(errors.port).toBe("Port must be between 1 and 65535.");
  });
});

describe("validateConfig — submit gate (Object.keys length)", () => {
  it("D1: a per-item map counts as a blocking error key", () => {
    const errors = validateConfig([nameServers], { nameServers: ["8.8.8.8", "foo_bar"] });
    expect(Object.keys(errors).length).toBeGreaterThan(0);
  });

  it("D2: all valid → no error keys, submit proceeds", () => {
    const errors = validateConfig([nameServers, port], { nameServers: ["8.8.8.8"], port: 443 });
    expect(Object.keys(errors).length).toBe(0);
  });
});
