import { onRequest } from "firebase-functions/v2/https";
import { db, FieldValue } from "./admin";

interface ValidateLicenseRequestBody {
  machineId?: string;
  licenseKey?: string;
  pluginVersion?: string;
}

export const validateLicense = onRequest(
  {
    region: "europe-west1",
    cors: true,
  },
  async (req, res) => {
    if (req.method !== "POST") {
      res.status(405).json({ valid: false, reason: "method_not_allowed" });
      return;
    }

    try {
      const { machineId, licenseKey, pluginVersion }: ValidateLicenseRequestBody = req.body || {};

      if (!machineId || typeof machineId !== "string" || !licenseKey || typeof licenseKey !== "string") {
        res.status(400).json({
          valid: false,
          reason: "invalid_request",
          message: "machineId and licenseKey are required",
        });
        return;
      }

      const cleanMachineId = machineId.trim();
      const cleanLicenseKey = licenseKey.trim();

      const licenseDocRef = db.collection("licenses").doc(cleanMachineId);
      const licenseDocSnap = await licenseDocRef.get();

      // 1. Doc doesn't exist
      if (!licenseDocSnap.exists) {
        res.status(200).json({ valid: false, reason: "not_found" });
        return;
      }

      const docData = licenseDocSnap.data();
      if (!docData) {
        res.status(200).json({ valid: false, reason: "not_found" });
        return;
      }

      // 2. Status check
      if (docData.status !== "active") {
        res.status(200).json({
          valid: false,
          reason: docData.status || "inactive",
        });
        return;
      }

      // 3. Expiration check
      if (docData.expiresAt) {
        let expiresMillis: number | null = null;
        if (typeof docData.expiresAt.toMillis === "function") {
          expiresMillis = docData.expiresAt.toMillis();
        } else if (docData.expiresAt instanceof Date) {
          expiresMillis = docData.expiresAt.getTime();
        } else if (typeof docData.expiresAt === "string" || typeof docData.expiresAt === "number") {
          expiresMillis = new Date(docData.expiresAt).getTime();
        }

        if (expiresMillis !== null && !isNaN(expiresMillis) && expiresMillis < Date.now()) {
          res.status(200).json({ valid: false, reason: "expired" });
          return;
        }
      }

      // 4. License key check
      if (docData.licenseKey !== cleanLicenseKey) {
        res.status(200).json({ valid: false, reason: "invalid_key" });
        return;
      }

      // 5. Update lastValidatedAt and pluginVersion if provided
      const updateData: Record<string, unknown> = {
        lastValidatedAt: FieldValue.serverTimestamp(),
      };
      if (pluginVersion && typeof pluginVersion === "string") {
        updateData.pluginVersion = pluginVersion.trim();
      }
      await licenseDocRef.update(updateData);

      // 6. Format expiresAt
      let expiresAtIso: string | null = null;
      if (docData.expiresAt) {
        if (typeof docData.expiresAt.toDate === "function") {
          expiresAtIso = docData.expiresAt.toDate().toISOString();
        } else if (docData.expiresAt instanceof Date) {
          expiresAtIso = docData.expiresAt.toISOString();
        } else if (typeof docData.expiresAt === "string") {
          expiresAtIso = new Date(docData.expiresAt).toISOString();
        }
      }

      res.status(200).json({
        valid: true,
        username: docData.username ?? "",
        expiresAt: expiresAtIso,
      });
    } catch (error) {
      console.error("Error validating license:", error);
      res.status(500).json({
        valid: false,
        reason: "internal_error",
        message: "Failed to validate license",
      });
    }
  }
);
