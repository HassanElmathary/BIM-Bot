import { onRequest } from "firebase-functions/v2/https";
import { db, FieldValue } from "./admin";

interface RequestActivationRequestBody {
  machineId?: string;
  username?: string;
  email?: string;
  machineName?: string;
  pluginVersion?: string;
}

export const requestActivation = onRequest(
  {
    region: "europe-west1",
    cors: true,
  },
  async (req, res) => {
    if (req.method !== "POST") {
      res.status(405).json({ success: false, message: "Method not allowed" });
      return;
    }

    try {
      const { machineId, username, email, machineName, pluginVersion }: RequestActivationRequestBody = req.body || {};

      if (!machineId || typeof machineId !== "string" || !username || typeof username !== "string" || !email || typeof email !== "string") {
        res.status(400).json({
          success: false,
          message: "machineId, username, and email are required fields",
        });
        return;
      }

      await db.collection("activation_requests").add({
        machineId: machineId.trim(),
        username: username.trim(),
        email: email.trim(),
        machineName: typeof machineName === "string" ? machineName.trim() : "",
        pluginVersion: typeof pluginVersion === "string" ? pluginVersion.trim() : "",
        requestedAt: FieldValue.serverTimestamp(),
        status: "pending",
      });

      res.status(200).json({
        success: true,
        message: "Request sent to admin",
      });
    } catch (error) {
      console.error("Error creating activation request:", error);
      res.status(500).json({
        success: false,
        message: "Failed to submit activation request",
      });
    }
  }
);
