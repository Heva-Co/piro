import type { Metadata } from "next";
import { Nav } from "@/src/components/Nav";
import { Footer } from "@/src/components/Footer";
import { ThemeProvider } from "@/src/components/ThemeProvider";
import "./globals.css";
import { PropsWithChildren } from "react";

export const metadata: Metadata = {
  title: "Status",
  description: "System status and uptime",
};


function RootLayout(props: PropsWithChildren) {

  const { children } = props;

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

export default RootLayout;
