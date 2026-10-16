import Link from "next/link";
import { redirect } from "next/navigation";
import { apiOrNull, getSessionUser } from "@/lib/api";
import AccountForm from "@/components/AccountForm";

export const dynamic = "force-dynamic";

/** Email-first preview backed by development authentication until an identity provider is added. */
export default async function SignInPage({ searchParams }: { searchParams: Promise<{ next?: string }> }) {
  if (await getSessionUser()) redirect("/me");
  const { next } = await searchParams;
  const users = (await apiOrNull<{ email: string; displayName: string }[]>("/v1/dev/users")) ?? [];

  return (
    <div className="panel signin-panel">
      <Link href="/" className="small">Back to browsing</Link>
      <h1>Welcome back</h1>
      <p className="muted">Sign in to bid, open auctions and manage your deals.</p>
      <AccountForm users={users} next={next ?? "/"} />
    </div>
  );
}
