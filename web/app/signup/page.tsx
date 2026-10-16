import Link from "next/link";
import { redirect } from "next/navigation";
import { getSessionUser } from "@/lib/api";
import AccountForm from "@/components/AccountForm";

export const dynamic = "force-dynamic";

export default async function SignUpPage({ searchParams }: { searchParams: Promise<{ next?: string }> }) {
  if (await getSessionUser()) redirect("/me");
  const { next } = await searchParams;

  return (
    <div className="panel signin-panel">
      <Link href="/" className="small">Back to browsing</Link>
      <h1>Create your account</h1>
      <p className="muted">Join Bidly to bid on nearby finds, open auctions and manage your deals.</p>
      <AccountForm next={next ?? "/me"} mode="signup" />
    </div>
  );
}
