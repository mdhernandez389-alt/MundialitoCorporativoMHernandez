import Link from "next/link";

const links = [
  { href: "/", label: "Tabla" },
  { href: "/scorers", label: "Goleadores" },
  { href: "/teams", label: "Equipos" },
  { href: "/matches", label: "Partidos" },
];

export function Nav() {
  return (
    <header className="border-b border-gray-200">
      <nav className="mx-auto flex max-w-5xl items-center gap-6 px-4 py-3">
        <span className="font-semibold">Mundialito Corporativo</span>
        {links.map((link) => (
          <Link key={link.href} href={link.href} className="text-sm hover:underline">
            {link.label}
          </Link>
        ))}
      </nav>
    </header>
  );
}
