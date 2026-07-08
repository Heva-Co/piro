import { type Id, toast } from "react-toastify";

const options = {
  autoClose: 7000,
  isLoading: false,
  closeOnClick: true,
};

export function showLoadingToast(children: string) {
  return toast.loading(() => children);
}

export function updateToastForSuccess(id: Id, children: string) {
  toast.update(id, { render: () => children, type: "success", ...options });
}

export function updateToastForError(id: Id, children: string) {
  toast.update(id, { render: () => children, type: "error", ...options });
}

export function showSuccessToast(children: string) {
  toast.success(() => children, {
    autoClose: 4000,
    closeOnClick: true,
    pauseOnHover: false,
    draggable: false,
  });
}

export function showErrorToast(children: string) {
  toast.error(() => children, {
    autoClose: 4000,
    closeOnClick: true,
    pauseOnHover: false,
    draggable: false,
  });
}

export function showInfoToast(children: string) {
  toast.info(() => children, {
    autoClose: 4000,
    closeOnClick: true,
    pauseOnHover: false,
    draggable: false,
  });
}
