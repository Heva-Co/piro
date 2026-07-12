import type { Metadata } from "next";
import { Nav } from "@/src/components/Nav";
import { Footer } from "@/src/components/Footer";
import { ThemeProvider } from "@/src/components/ThemeProvider";
import "./globals.css";

export const metadata: Metadata = {
  title: "Status",
  description: "System status and uptime",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className="h-full antialiased" suppressHydrationWarning>
      <body className="min-h-full flex flex-col">
        <ThemeProvider>
          <Nav />
          {children}
          <Footer />
        </ThemeProvider>
      </body>
    </html>
  );
}
