import type { UserDto } from "$lib/api";

declare global {
  namespace App {
    interface Locals {
      user?: UserDto;
      accessToken?: string;
    }
    interface PageData {
      user?: UserDto;
    }
  }
}

export {};
