import * as crypto from "crypto";
import { onRequest } from "firebase-functions/v2/https";
import { db, FieldValue } from "./admin";

/**
 * Compares two secrets in constant time so a caller cannot recover the secret
 * by measuring how long a rejection takes.
 */
function secretMatches(supplied: string, expected: string): boolean {
  const a = Buffer.from(supplied, "utf8");
  const b = Buffer.from(expected, "utf8");
  if (a.length !== b.length) return false;
  return crypto.timingSafeEqual(a, b);
}

interface GenerateLicenseRequestBody {
  machineId?: string;
  username?: string;
  email?: string;
  adminSecret?: string;
}

function generateLicenseCode(): string {
  const hex = crypto.randomBytes(8).toString("hex").toUpperCase();
  return `BIMBOT-${hex.slice(0, 4)}-${hex.slice(4, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}`;
}

export const generateLicenseKey = onRequest(
  {
    region: "europe-west1",
    // This endpoint mints licenses. It is called by the admin tooling, never by
    // a browser, so no cross-origin access is granted.
    cors: false,
    secrets: ["BIMBOT_ADMIN_SECRET"],
  },
  async (req, res) => {
    if (req.method !== "POST") {
      res.status(405).json({ success: false, error: "Method not allowed" });
      return;
    }

    try {
      const expectedSecret = process.env.BIMBOT_ADMIN_SECRET;
      if (!expectedSecret) {
        console.error("BIMBOT_ADMIN_SECRET is not configured - refusing to issue licenses.");
        res.status(500).json({ success: false, error: "Server authentication is misconfigured" });
        return;
      }

      const { machineId, username, email, adminSecret }: GenerateLicenseRequestBody = req.body || {};

      // 1. Validate admin secret
      if (!adminSecret || typeof adminSecret !== "string" || !secretMatches(adminSecret, expectedSecret)) {
        res.status(401).json({ success: false, error: "Unauthorized: Invalid admin secret" });
        return;
      }

      // 2. Validate required fields
      if (!machineId || typeof machineId !== "string" || !username || typeof username !== "string" || !email || typeof email !== "string") {
        res.status(400).json({
          success: false,
          error: "machineId, username, and email are required fields",
        });
        return;
      }

      const cleanMachineId = machineId.trim();
      const cleanUsername = username.trim();
      const cleanEmail = email.trim();

      // 3. Generate license key
      const licenseKey = generateLicenseCode();

      // 4. Write/update Firestore doc at licenses/{machineId}
      const licenseDocRef = db.collection("licenses").doc(cleanMachineId);
      await licenseDocRef.set({
        username: cleanUsername,
        email: cleanEmail,
        machineId: cleanMachineId,
        licenseKey,
        status: "active",
        activatedAt: FieldValue.serverTimestamp(),
        expiresAt: null,
        lastValidatedAt: FieldValue.serverTimestamp(),
      }, { merge: true });

      // 5. Return success
      res.status(200).json({
        success: true,
        licenseKey,
      });
    } catch (error) {
      console.error("Error generating license key:", error);
      res.status(500).json({
        success: false,
        error: "Failed to generate license key",
      });
    }
  }
);
