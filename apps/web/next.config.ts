import type { NextConfig } from "next";

const API_URL = process.env.INTERNAL_API_URL ?? "http://localhost:5117";

const nextConfig: NextConfig = {
  output: "standalone",
  async rewrites() {
    return [
      {
        source: "/api/v1/:path*",
        destination: `${API_URL}/api/v1/:path*`,
      },
    ];
  },
};

export default nextConfig;
